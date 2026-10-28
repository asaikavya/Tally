using Microsoft.EntityFrameworkCore;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.PayPal;

namespace Tally.Infrastructure.Receipts;

public sealed record ApproveRequest(Guid PayerId, List<Guid> ChargeIds);
public sealed record AppliedLine(Guid ChargeId, string Student, string Offering, decimal Applied, decimal StillDue);
public sealed record ApproveResult(Guid PaymentId, decimal Amount, decimal Applied, decimal Credit,
                                   List<AppliedLine> Lines, List<PayPalStep> PayPal);

public sealed record ApprovalOutcome(bool Found, string? Problem, ApproveResult? Result)
{
    public static ApprovalOutcome NotFound() => new(false, null, null);
    public static ApprovalOutcome Fail(string problem) => new(true, problem, null);
    public static ApprovalOutcome Ok(ApproveResult r) => new(true, null, r);
}

// The owner said yes. Tally re-checks everything, writes the money in one save, then notes it on PayPal.
public sealed class ApprovalService(TallyDbContext db, PayPalNoteService paypalNotes)
{
    public async Task<ApprovalOutcome> ApproveAsync(Guid receiptId, ApproveRequest req, CancellationToken ct)
    {
        var r = await db.Receipts.FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return ApprovalOutcome.NotFound();
        if (r.Status == ReceiptStatus.Matched) return ApprovalOutcome.Fail("This receipt is already approved.");
        if (r.Status is not (ReceiptStatus.Read or ReceiptStatus.NeedsReview))
            return ApprovalOutcome.Fail("This receipt can't be approved (it's a duplicate or was rejected).");
        if (r.Amount is not > 0) return ApprovalOutcome.Fail("Type the amount first.");

        var ids = req.ChargeIds.Distinct().ToList();
        if (ids.Count == 0) return ApprovalOutcome.Fail("Pick at least one class.");

        var charges = await db.Charges
            .Include(c => c.Enrollment!).ThenInclude(e => e.Member)
            .Include(c => c.Enrollment!).ThenInclude(e => e.PricePlan!).ThenInclude(p => p.Offering)
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct);

        if (charges.Count != ids.Count) return ApprovalOutcome.Fail("Some of those classes no longer exist.");
        if (charges.Any(c => c.Enrollment!.Member!.PayerId != req.PayerId))
            return ApprovalOutcome.Fail("Those classes belong to a different family.");
        if (charges.Any(c => c.Remaining <= 0)) return ApprovalOutcome.Fail("One of those classes is already paid.");

        var payment = new Payment
        {
            Source = ToSource(r.Method),
            Amount = r.Amount.Value,
            Currency = "USD",
            ReceivedAtUtc = r.PaidOn is { } d ? d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : DateTime.UtcNow,
            SenderText = r.SenderName,
            ExternalId = r.Reference,
            EvidenceUrl = $"/api/receipts/{r.Id}/image",
            PayerId = req.PayerId,
            ReceiptId = r.Id,
        };
        db.Payments.Add(payment);

        var left = payment.Amount;
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

        r.Status = ReceiptStatus.Matched;
        r.PayerId = req.PayerId;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return ApprovalOutcome.Fail("Someone changed this bill a moment ago. Refresh and try again.");
        }

        await paypalNotes.RecordAsync(payment.Id, ct);       // Tally is saved; now the PayPal bill
        return ApprovalOutcome.Ok((await GetAsync(r.Id, ct))!);
    }

    public async Task<ApprovalOutcome> UndoAsync(Guid receiptId, CancellationToken ct)
    {
        var r = await db.Receipts.FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return ApprovalOutcome.NotFound();

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.ReceiptId == receiptId, ct);
        if (payment is null || r.Status != ReceiptStatus.Matched)
            return ApprovalOutcome.Fail("This receipt isn't approved, so there's nothing to undo.");

        // PayPal first: if PayPal says no, we undo nothing, so Tally and PayPal never disagree.
        if (await paypalNotes.RemoveAsync(payment.Id, ct) is { } problem) return ApprovalOutcome.Fail(problem);

        var allocs = await db.Allocations.Where(a => a.PaymentId == payment.Id).ToListAsync(ct);
        var chargeIds = allocs.Select(a => a.ChargeId).ToList();
        var charges = await db.Charges.Where(c => chargeIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        foreach (var a in allocs) charges[a.ChargeId].ReversePayment(a.Amount);
        db.Allocations.RemoveRange(allocs);
        db.Payments.Remove(payment);
        r.Status = ReceiptStatus.Read;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return ApprovalOutcome.Fail("Someone changed this bill a moment ago. Refresh and try again.");
        }
        return ApprovalOutcome.Ok(new ApproveResult(payment.Id, payment.Amount, 0, 0, [], []));
    }

    // "Try PayPal again" after PayPal was down during Approve.
    public async Task<ApprovalOutcome> RetryPayPalAsync(Guid receiptId, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.ReceiptId == receiptId, ct);
        if (payment is null) return ApprovalOutcome.Fail("This receipt isn't approved yet.");
        await paypalNotes.RecordAsync(payment.Id, ct);
        return ApprovalOutcome.Ok((await GetAsync(receiptId, ct))!);
    }

    public async Task<ApproveResult?> GetAsync(Guid receiptId, CancellationToken ct)
    {
        var paymentId = await db.Payments.AsNoTracking()
            .Where(p => p.ReceiptId == receiptId).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        return paymentId is null ? null : await DescribePaymentAsync(paymentId.Value, ct);
    }

    // What one payment was applied to, plus its PayPal notes. Used by receipts and by hand-recorded payments.
    public async Task<ApproveResult?> DescribePaymentAsync(Guid paymentId, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return null;

        var lines = await (
            from a in db.Allocations.AsNoTracking()
            join c in db.Charges on a.ChargeId equals c.Id
            where a.PaymentId == payment.Id
            orderby c.DueDate, c.Enrollment!.Member!.Name, c.Enrollment!.PricePlan!.Offering!.Name
            select new AppliedLine(a.ChargeId, c.Enrollment!.Member!.Name, c.Enrollment!.PricePlan!.Offering!.Name,
                                   a.Amount, c.AmountDue - c.AmountPaid))
            .ToListAsync(ct);

        var applied = lines.Sum(l => l.Applied);
        var steps = await paypalNotes.DescribeAsync(payment.Id, ct);
        return new ApproveResult(payment.Id, payment.Amount, applied, payment.Amount - applied, lines, steps);
    }

    private static PaymentSource ToSource(string? method) => method?.Trim().ToLowerInvariant() switch
    {
        var m when m is not null && m.Contains("zelle") => PaymentSource.Zelle,
        var m when m is not null && m.Contains("upi") => PaymentSource.Upi,
        var m when m is not null && m.Contains("cash") => PaymentSource.Cash,
        var m when m is not null && m.Contains("check") => PaymentSource.Check,
        _ => PaymentSource.Other,
       
    };
}