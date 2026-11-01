using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tally.Infrastructure.Demo;
using Tally.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;

namespace Tally.Api.Controllers;


[ApiController]
[Route("api/demo")]
public sealed class DemoController(DemoResetService reset, IOptions<DemoOptions> options,TallyDbContext db) : ControllerBase
{
    private static readonly SemaphoreSlim Gate = new(1, 1);   // one reset at a time

    // POST api/demo/reset
    [HttpPost("reset")]
    public async Task<ActionResult<DemoResetResult>> Reset()
    {
        if (!options.Value.Enabled) return NotFound();
        if (!await Gate.WaitAsync(0)) return Conflict(new { message = "A reset is already running." });
        try
        {
            // CancellationToken.None: once started, finish — a half-reset is worse than a slow one.
            return await reset.ResetAsync(CancellationToken.None);
        }
        finally
        {
            Gate.Release();
        }
    }
    [HttpPost("sync")]       // ← new one goes here, inside the class
    public async Task<IActionResult> Sync([FromServices] PayPalEventHandler handler, CancellationToken ct)
       => Ok(new { checkedInvoices = await handler.SyncOpenInvoicesAsync(ct) });
    // GET api/demo/progress: which story steps are done since the last reset. Read from real data, so steps
    // tick however the visitor did them (any page, any tab).

    // POST api/demo/fresh-if-idle: called when the app opens. If the shared demo was used and then left
    // alone, start it fresh so the next visitor begins at step 1.
    [HttpPost("fresh-if-idle")]
    public async Task<IActionResult> FreshIfIdle(CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled) return NotFound();
        if (o.IdleResetMinutes <= 0) return Ok(new { reset = false });

        var last = DemoActivity.LastUtc;
        if (last is not null && DateTime.UtcNow - last < TimeSpan.FromMinutes(o.IdleResetMinutes))
            return Ok(new { reset = false });                       // someone is using it right now

        if (!await WasUsedAsync(ct)) return Ok(new { reset = false });   // already fresh: nothing to do

        if (!await Gate.WaitAsync(0)) return Ok(new { reset = false });  // another reset is running
        try
        {
            var result = await reset.ResetAsync(CancellationToken.None);
            return Ok(new { reset = true, result });
        }
        finally
        {
            Gate.Release();
        }
    }

    // Has any visitor changed the demo since the last reset? (Seed data alone doesn't count.)
    private async Task<bool> WasUsedAsync(CancellationToken ct) =>
        await db.Receipts.AnyAsync(ct)
        || await db.ReminderLogs.AnyAsync(ct)
        || await db.Charges.AnyAsync(c => c.ParentSaysPaidAtUtc != null, ct)
        || await db.Payments.AnyAsync(p => p.Source != PaymentSource.PayPal && p.PayerId != null, ct)
        // A PayPal payment for a family that wasn't already paid in the seed (Meera, Lakshmi).
        || await db.Allocations.AnyAsync(a =>
               db.Payments.Any(p => p.Id == a.PaymentId && p.Source == PaymentSource.PayPal) &&
               db.Charges.Any(c => c.Id == a.ChargeId &&
                   db.Payers.Any(p => p.Id == c.Enrollment!.Member!.PayerId &&
                                      !p.Name.StartsWith("Meera") && !p.Name.StartsWith("Lakshmi"))), ct);

}