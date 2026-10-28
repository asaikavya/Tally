using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;

namespace Tally.Infrastructure.PayPal;

public sealed record PayPalStep(string PayPalInvoiceId, decimal Amount, bool Noted, string? StatusBefore, string? StatusAfter);

// Keeps the PayPal bill in step with what the owner approved in Tally.
public sealed class PayPalNoteService(TallyDbContext db, PayPalInvoiceClient paypal, ILogger<PayPalNoteService> log)
{
    private sealed record Touched(Guid InvoiceId, string PayPalInvoiceId, string PayPalStatus, decimal Amount, List<string> Lines);

    // Which PayPal bills this payment touched, and how much went to each.
    private async Task<List<Touched>> TouchedAsync(Guid paymentId, CancellationToken ct)
    {
        var rows = await (
            from a in db.Allocations.AsNoTracking()
            join c in db.Charges on a.ChargeId equals c.Id
            join i in db.Invoices on c.InvoiceId equals (Guid?)i.Id
            where a.PaymentId == paymentId
            select new
            {
                InvoiceId = i.Id,
                i.PayPalInvoiceId,
                i.PayPalStatus,
                a.Amount,
                Student = c.Enrollment!.Member!.Name,
                Offering = c.Enrollment!.PricePlan!.Offering!.Name,
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => (r.InvoiceId, r.PayPalInvoiceId, r.PayPalStatus))
            .Select(g => new Touched(g.Key.InvoiceId, g.Key.PayPalInvoiceId, g.Key.PayPalStatus, g.Sum(x => x.Amount),
                g.Select(x => $"{x.Student.Split(' ')[0]} {x.Offering} ${x.Amount.ToString("0.00", CultureInfo.InvariantCulture)}").ToList()))
            .ToList();
    }

    // Note the payment on every PayPal bill it touched. Safe to call again: skips bills already noted.
    public async Task RecordAsync(Guid paymentId, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstAsync(p => p.Id == paymentId, ct);
        var receipt = payment.ReceiptId is { } rid
            ? await db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid, ct)
            : null;
        var already = await db.PayPalNotes.Where(n => n.PaymentId == paymentId).Select(n => n.InvoiceId).ToListAsync(ct);

        foreach (var t in await TouchedAsync(paymentId, ct))
        {
            if (already.Contains(t.InvoiceId)) continue;
            try
            {
                var paidOn = DateOnly.FromDateTime(payment.ReceivedAtUtc);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var body = new RecordPaymentRequest(
                    ToPayPalMethod(payment.Source, receipt?.Method),
                    (paidOn > today ? today : paidOn).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    new RecordedAmount("USD", t.Amount.ToString("0.00", CultureInfo.InvariantCulture)),
                    BuildNote(payment, receipt, t));

                var payPalPaymentId = await paypal.RecordPaymentAsync(t.PayPalInvoiceId, body, $"tally-{paymentId}-{t.InvoiceId}", ct);

                var note = new PayPalNote
                {
                    PaymentId = paymentId,
                    InvoiceId = t.InvoiceId,
                    PayPalInvoiceId = t.PayPalInvoiceId,
                    PayPalPaymentId = payPalPaymentId,
                    Amount = t.Amount,
                    StatusBefore = t.PayPalStatus,
                    StatusAfter = t.PayPalStatus,
                    RecordedAtUtc = DateTime.UtcNow,
                };
                db.PayPalNotes.Add(note);
                await db.SaveChangesAsync(ct);                 // save PayPal's id first, so Undo can always find it

                note.StatusAfter = await RefreshStatusAsync(t.InvoiceId, t.PayPalInvoiceId, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (HttpRequestException ex)
            {
                log.LogWarning(ex, "Couldn't note payment {PaymentId} on PayPal invoice {Invoice}", paymentId, t.PayPalInvoiceId);
            }
        }
    }

    // Undo on PayPal's side. Returns a problem message, or null when everything was removed.
    public async Task<string?> RemoveAsync(Guid paymentId, CancellationToken ct)
    {
        var notes = await db.PayPalNotes.Where(n => n.PaymentId == paymentId).ToListAsync(ct);
        foreach (var n in notes)
        {
            try
            {
                await paypal.DeletePaymentAsync(n.PayPalInvoiceId, n.PayPalPaymentId, ct);
                db.PayPalNotes.Remove(n);
                await db.SaveChangesAsync(ct);
                await RefreshStatusAsync(n.InvoiceId, n.PayPalInvoiceId, ct);
            }
            catch (HttpRequestException ex)
            {
                log.LogWarning(ex, "Couldn't remove PayPal payment {PayPalPaymentId}", n.PayPalPaymentId);
                return "PayPal didn't remove the note, so nothing was undone. Try again in a moment.";
            }
        }
        return null;
    }

    // For the screen: one line per PayPal bill this payment touched.
    public async Task<List<PayPalStep>> DescribeAsync(Guid paymentId, CancellationToken ct)
    {
        var touched = await TouchedAsync(paymentId, ct);
        var notes = await db.PayPalNotes.AsNoTracking().Where(n => n.PaymentId == paymentId).ToListAsync(ct);
        return touched.Select(t => notes.FirstOrDefault(n => n.InvoiceId == t.InvoiceId) is { } n
                ? new PayPalStep(t.PayPalInvoiceId, n.Amount, true, n.StatusBefore, n.StatusAfter)
                : new PayPalStep(t.PayPalInvoiceId, t.Amount, false, t.PayPalStatus, null))
            .ToList();
    }

    // Ask PayPal for the official status and remember it.
    private async Task<string> RefreshStatusAsync(Guid invoiceId, string payPalInvoiceId, CancellationToken ct)
    {
        var fresh = await paypal.GetAsync(payPalInvoiceId, ct);
        var invoice = await db.Invoices.FirstAsync(i => i.Id == invoiceId, ct);
        invoice.PayPalStatus = fresh.Status;
        await db.SaveChangesAsync(ct);
        return fresh.Status;
    }

    private static string ToPayPalMethod(PaymentSource source, string? method) => source switch
    {
        PaymentSource.Zelle or PaymentSource.Upi => "BANK_TRANSFER",
        PaymentSource.Cash => "CASH",
        PaymentSource.Check => "CHECK",
        _ when method?.Contains("check", StringComparison.OrdinalIgnoreCase) == true => "CHECK",
        _ when method?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true => "BANK_TRANSFER",
        _ => "OTHER",
    };

    // e.g. "Zelle from R KUMAR on Nov 9 · Ananya Dance $90.00 + Arjun Guitar $80.00 · recorded by Tally"
    private static string BuildNote(Payment p, Receipt? r, Touched t)
    {
        var how = p.Source switch
        {
            PaymentSource.Zelle => "Zelle",
            PaymentSource.Upi => "UPI",
            PaymentSource.Cash => "Cash",
            PaymentSource.Check => "CHECK",
            _ => string.IsNullOrWhiteSpace(r?.Method) ? "Outside payment" : r!.Method!,
        };
        var from = string.IsNullOrWhiteSpace(p.SenderText) ? "" : $" from {p.SenderText}";
        var on = p.ReceivedAtUtc.ToString("MMM d", CultureInfo.InvariantCulture);
        var reference = string.IsNullOrWhiteSpace(p.ExternalId) ? "" : $" · ref {p.ExternalId}";
        return $"{how}{from} on {on} · {string.Join(" + ", t.Lines)}{reference} · recorded by Tally";
    }
}