using Microsoft.EntityFrameworkCore;
using Tally.Domain.Matching;
using Tally.Infrastructure.Data;

namespace Tally.Infrastructure.Receipts;

// Loads what the Detective needs from SQL, then lets the pure Domain code decide.
public sealed class DetectiveService(TallyDbContext db)
{
    public async Task<DetectiveResult?> InvestigateAsync(Guid receiptId, CancellationToken ct)
    {
        var r = await db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return null;
        if (r.Amount is not > 0) return new(Verdict.NoMatch, "Type the amount first", null, [], []);

        var payers = await db.Payers.AsNoTracking().Select(p => new { p.Id, p.Name }).ToListAsync(ct);
        var aliases = await db.PayerAliases.AsNoTracking().Select(a => new { a.PayerId, a.AliasText }).ToListAsync(ct);
        var payerInfos = payers
            .Select(p => new PayerInfo(p.Id, p.Name, aliases.Where(a => a.PayerId == p.Id).Select(a => a.AliasText).ToList()))
            .ToList();

        var open = await db.Charges.AsNoTracking()
            .Where(c => c.AmountDue > c.AmountPaid)
            .Select(c => new OpenCharge(
                c.Id,
                c.Enrollment!.Member!.PayerId,
                c.Enrollment!.Member!.Name,
                c.Enrollment!.PricePlan!.Offering!.Name,
                c.AmountDue - c.AmountPaid,
                c.DueDate))
            .ToListAsync(ct);

        // Past payments with a sender name, per family (for "paid as R KUMAR in October").
        var past = await (
            from a in db.Allocations.AsNoTracking()
            join p in db.Payments on a.PaymentId equals p.Id
            join c in db.Charges on a.ChargeId equals c.Id
            where p.SenderText != null
            select new { PayerId = c.Enrollment!.Member!.PayerId, p.SenderText, p.Amount, p.ReceivedAtUtc })
            .Distinct()
            .ToListAsync(ct);
        var history = past.Select(h => new PastPayment(h.PayerId, h.SenderText, h.Amount, DateOnly.FromDateTime(h.ReceivedAtUtc))).ToList();

        var facts = new ReceiptFacts(r.Amount.Value, r.SenderName, r.Memo, r.PaidOn, r.UploadedBy == "parent" ? r.PayerId : null);
        return PaymentDetective.Investigate(facts, payerInfos, open, history);
    }
}