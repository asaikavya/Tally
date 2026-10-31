using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Demo;

namespace Tally.Api.Controllers;

// Owner-only links for ONE family at a time. Never included in lists.
[ApiController]
[Route("api/payers")]
public sealed class PayersController(TallyDbContext db, IOptions<DemoOptions> demo, ILogger<PayersController> log) : ControllerBase
{
    // The family's private Tally page (where they tap "I already paid").
    [HttpPost("{id:guid}/pay-link")]
    public async Task<IActionResult> PayLink(Guid id, CancellationToken ct)
    {
        if (!CanSeeLinks()) return StatusCode(403, new { problem = "Sign in as the owner to see family links." });

        var token = await db.Payers.AsNoTracking().Where(p => p.Id == id).Select(p => p.PayToken).FirstOrDefaultAsync(ct);
        if (token is null) return NotFound();

        log.LogInformation("Pay link viewed for payer {PayerId}", id);
        return Ok(new { url = $"/pay/{token}" });
    }

    // If a link ever leaks: give the family a new one. The old link stops working at once.
    [HttpPost("{id:guid}/pay-link/reset")]
    public async Task<IActionResult> ResetLink(Guid id, CancellationToken ct)
    {
        if (!CanSeeLinks()) return StatusCode(403, new { problem = "Sign in as the owner to change family links." });

        var payer = await db.Payers.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payer is null) return NotFound();

        payer.PayToken = Payer.NewToken();
        await db.SaveChangesAsync(ct);
        log.LogInformation("Pay link reset for payer {PayerId}", id);
        return Ok(new { url = $"/pay/{payer.PayToken}" });
    }

    // The family's CURRENT PayPal bill (always the newest, so a demo reset can't leave a stale link).
    [HttpPost("{id:guid}/paypal-bill")]
    public async Task<IActionResult> PayPalBill(Guid id, CancellationToken ct)
    {
        if (!CanSeeLinks()) return StatusCode(403, new { problem = "Sign in as the owner to see PayPal bills." });

        var bill = await db.Invoices.AsNoTracking()
            .Where(i => i.PayerId == id && i.PayUrl != null)
            .OrderByDescending(i => i.PeriodStart).ThenByDescending(i => i.SentAtUtc)
            .Select(i => new { url = i.PayUrl, status = i.PayPalStatus, payPalInvoiceId = i.PayPalInvoiceId })
            .FirstOrDefaultAsync(ct);

        return bill is null ? NotFound(new { problem = "This family doesn't have a PayPal bill yet." }) : Ok(bill);
    }

    // Until owner login exists, links are only shown for the demo academy's fake families.
    private bool CanSeeLinks() => User.Identity?.IsAuthenticated == true || demo.Value.Enabled;
}