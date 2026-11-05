using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Demo;

namespace Tally.Api.Controllers;

public sealed record DemoProgressDto(
    bool ZelleRead,         // 1. a receipt was uploaded and read
    bool NotedOnPayPal,     // 2. an approved receipt was noted on a PayPal bill
    bool ParentSaidPaid,    // 3a. Ravi tapped "I already paid"
    bool ParentConfirmed,   // 3b. owner confirmed Ravi's payment
    bool ReminderSent,      // 4. a reminder went to Lucia
    bool PaidWithPayPal,    // 5. Lucia paid her PayPal bill (arrived by webhook)
    Guid? RaviPayerId,
    Guid? LuciaPayerId,
    decimal OutsideTotal,   // Zelle, check, cash… recorded since the reset
    decimal PayPalTotal,    // Lucia's PayPal payment, applied to her bill
    int PayPalNotes,        // notes Tally wrote on PayPal bills
    decimal RaviOwes,       // what Ravi still owes: 0 means the story can't continue for him
    decimal LuciaOwes);     // what Lucia still owes

// The demo guide asks "which steps are done?" The answer comes from real data, never a saved checklist.
[ApiController]
[Route("api/demo")]
public sealed class DemoProgressController(TallyDbContext db, IOptions<DemoOptions> demo) : ControllerBase
{
    [HttpGet("progress")]
    public async Task<ActionResult<DemoProgressDto>> Progress(CancellationToken ct)
    {
        if (!demo.Value.Enabled) return NotFound();

        var people = await db.Payers.AsNoTracking()
            .Where(p => p.Name.StartsWith("Ravi") || p.Name.StartsWith("Lucia"))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);
        var ravi = people.FirstOrDefault(p => p.Name.StartsWith("Ravi"))?.Id;
        var lucia = people.FirstOrDefault(p => p.Name.StartsWith("Lucia"))?.Id;

        var zelleRead = await db.Receipts.AnyAsync(ct);

        var notedOnPayPal = await db.PayPalNotes.AnyAsync(n =>
            db.Payments.Any(p => p.Id == n.PaymentId && p.ReceiptId != null), ct);

        var parentSaidPaid = ravi != null && await db.Charges.AnyAsync(c =>
            c.Enrollment!.Member!.PayerId == ravi && c.ParentSaysPaidAtUtc != null, ct);

        var parentConfirmed = ravi != null && await db.Payments.AnyAsync(p =>
            p.PayerId == ravi && p.ReceiptId == null && p.Source != PaymentSource.PayPal, ct);

        var reminderSent = lucia != null && await db.ReminderLogs.AnyAsync(l => l.PayerId == lucia, ct);

        // A PayPal payment applied to one of Lucia's bills: only the webhook creates that.
        var paidWithPayPal = lucia != null && await db.Allocations.AnyAsync(a =>
            db.Payments.Any(p => p.Id == a.PaymentId && p.Source == PaymentSource.PayPal) &&
            db.Charges.Any(c => c.Id == a.ChargeId && c.Enrollment!.Member!.PayerId == lucia), ct);

        var outsideTotal = await db.Payments
            .Where(p => p.Source != PaymentSource.PayPal && p.PayerId != null)   // visitor's payments, not seeded history
            .SumAsync(p => p.Amount, ct);

        var payPalTotal = lucia == null ? 0 : await db.Allocations
            .Where(a => db.Payments.Any(p => p.Id == a.PaymentId && p.Source == PaymentSource.PayPal) &&
                        db.Charges.Any(c => c.Id == a.ChargeId && c.Enrollment!.Member!.PayerId == lucia))
            .SumAsync(a => a.Amount, ct);

        var payPalNotes = await db.PayPalNotes.CountAsync(ct);

        var raviOwes = await OwesAsync(ravi, ct);
        var luciaOwes = await OwesAsync(lucia, ct);

        return new DemoProgressDto(zelleRead, notedOnPayPal, parentSaidPaid, parentConfirmed,
                                   reminderSent, paidWithPayPal, ravi, lucia,
                                   outsideTotal, payPalTotal, payPalNotes, raviOwes, luciaOwes);
    }

    // Everything still open for one family.
    private async Task<decimal> OwesAsync(Guid? payerId, CancellationToken ct) =>
        payerId is null ? 0 : await db.Charges
            .Where(c => c.Enrollment!.Member!.PayerId == payerId && c.AmountDue > c.AmountPaid)
            .SumAsync(c => c.AmountDue - c.AmountPaid, ct);
}
