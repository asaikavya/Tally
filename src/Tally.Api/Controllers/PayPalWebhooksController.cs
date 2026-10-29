using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Messaging;
using Tally.Infrastructure.PayPal;

namespace Tally.Api.Controllers;

[ApiController]
[Route("api/webhooks/paypal")]
public class PayPalWebhooksController(
    TallyDbContext db, PayPalInvoiceClient paypal, IWebhookQueue queue,
    IOptions<PayPalOptions> options, IWebHostEnvironment env, ILogger<PayPalWebhooksController> log) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        log.LogInformation("PayPal webhook received ({Length} bytes)", Request.ContentLength);
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        // 1. Check who's ringing
        var skip = env.IsDevelopment() && options.Value.SkipWebhookVerification;
        if (!skip)
        {
            var headers = new WebhookHeaders(H("PAYPAL-AUTH-ALGO"), H("PAYPAL-CERT-URL"),
                H("PAYPAL-TRANSMISSION-ID"), H("PAYPAL-TRANSMISSION-SIG"), H("PAYPAL-TRANSMISSION-TIME"));

            // Missing headers = not from PayPal. Don't even ask PayPal.
            if (string.IsNullOrEmpty(headers.TransmissionId) || string.IsNullOrEmpty(headers.TransmissionSig))
            {
                log.LogWarning("Rejected a webhook without PayPal signature headers");
                return BadRequest();
            }

            bool valid;
            try { valid = await paypal.VerifyWebhookAsync(headers, body, options.Value.WebhookId, ct); }
            catch (HttpRequestException ex)
            {
                log.LogWarning(ex, "PayPal couldn't verify a webhook signature");
                return BadRequest();
            }
            if (!valid)
            {
                log.LogWarning("Rejected a PayPal webhook with an invalid signature");
                return BadRequest();
            }
        }

        // 2. Never answer the same ring twice
        // Not JSON, or not shaped like a PayPal event → 400, never a crash.
        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (JsonException)
        {
            log.LogWarning("Rejected a webhook that isn't JSON");
            return BadRequest();
        }
        using var _ = doc;

        if (!doc.RootElement.TryGetProperty("id", out var idProp) || idProp.GetString() is not { Length: > 0 } eventId
            || !doc.RootElement.TryGetProperty("event_type", out var typeProp) || typeProp.GetString() is not { Length: > 0 } eventType)
        {
            log.LogWarning("Rejected a webhook without an id or event_type");
            return BadRequest();
        }
        if (await db.WebhookEvents.AnyAsync(e => e.PayPalEventId == eventId, ct)) return Ok();

        var evt = new WebhookEvent
        {
            PayPalEventId = eventId,
            EventType = eventType,
            Payload = body,
            ReceivedAtUtc = DateTime.UtcNow
        };
        db.WebhookEvents.Add(evt);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Ok(); }     // two deliveries raced; the unique index kept only one

        // 3. Say "got it" quickly, then do the work
        await queue.EnqueueAsync(evt.Id, ct);
        return Ok();
    }

    private string H(string name) => Request.Headers[name].ToString();
}