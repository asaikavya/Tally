using Microsoft.EntityFrameworkCore;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.PayPal;
using Tally.Infrastructure.Receipts;

namespace Tally.Infrastructure.Payments;

public sealed record ManualPaymentRequest(Guid PayerId, decimal Amount, string Method);

// The owner says "I got it" with no receipt: cash in hand, a check, or confirming a parent's "I already paid".
public sealed class ManualPaymentService(TallyDbContext db, PayPalNoteService paypalNotes, ApprovalService approvals)
{
    private static readonly string[] Methods = ["Zelle", "UPI", "Cash", "Check", "Other"];

    public async Task<ApprovalOutcome> RecordAsync(ManualPaymentRequest req, CancellationToken ct)
    {
        if (req.Amount is <= 0 or > 10_000) return ApprovalOutcome.Fail("Enter an amount between $0.01 and $10,000.");
        if (!Methods.Contains(req.Method)) return ApprovalOutcome.Fail("Pick how they paid.");

        var charges = await db.Charges
            .Include(c => c.Enrollment!).ThenInclude(e => e.Member)
            .Include(c => c.Enrollment!).ThenInclude(e => e.PricePlan!).ThenInclude(p => p.Offering)
            .Where(c => c.Enrollment!.Member!.PayerId == req.PayerId && c.AmountDue > c.AmountPaid)
            .ToListAsync(ct);
        if (charges.Count == 0) return ApprovalOutcome.Fail("This family has nothing open.");

        var payment = new Payment
        {
            Source = req.Method switch
            {
                "Zelle" => PaymentSource.Zelle,
                "UPI" => PaymentSource.Upi,
                "Cash" => PaymentSource.Cash,
                "Check" => PaymentSource.Check,
                _ => PaymentSource.Other,
            },
            Amount = decimal.Round(req.Amount, 2),
            Currency = "USD",
            ReceivedAtUtc = DateTime.UtcNow,
            PayerId = req.PayerId,
        };
        db.Payments.Add(payment);

        var left = payment.Amount;                      // oldest bill first, same order as everywhere else
        foreach (var c in charges.OrderBy(c => c.DueDate)
                                 .ThenBy(c => c.Enrollment!.Member!.Name)
                                 .ThenBy(c => c.Enrollment!.PricePlan!.Offering!.Name))
        {
            if (left <= 0) break;
            var take = Math.Min(left, c.Remaining);
            c.ApplyPayment(take);
            db.Allocations.Add(new Allocation { PaymentId = payment.Id, ChargeId = c.Id, Amount = take });
            left -= take;
        }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return ApprovalOutcome.Fail("Someone changed this bill a moment ago. Refresh and try again.");
        }

        await paypalNotes.RecordAsync(payment.Id, ct);
        return ApprovalOutcome.Ok((await approvals.DescribePaymentAsync(payment.Id, ct))!);
    }

    public async Task<ApprovalOutcome> UndoAsync(Guid paymentId, CancellationToken ct)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return ApprovalOutcome.NotFound();
        if (payment.ReceiptId is not null || payment.Source == PaymentSource.PayPal)
            return ApprovalOutcome.Fail("Only payments recorded by hand can be undone here.");

        // PayPal first, exactly like receipt Undo: if PayPal says no, nothing changes.
        if (await paypalNotes.RemoveAsync(payment.Id, ct) is { } problem) return ApprovalOutcome.Fail(problem);

        var allocs = await db.Allocations.Where(a => a.PaymentId == payment.Id).ToListAsync(ct);
        var ids = allocs.Select(a => a.ChargeId).ToList();
        var charges = await db.Charges.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        foreach (var a in allocs) charges[a.ChargeId].ReversePayment(a.Amount);
        db.Allocations.RemoveRange(allocs);
        db.Payments.Remove(payment);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return ApprovalOutcome.Fail("Someone changed this bill a moment ago. Refresh and try again.");
        }
        return ApprovalOutcome.Ok(new ApproveResult(payment.Id, payment.Amount, 0, 0, [], []));
    }
}