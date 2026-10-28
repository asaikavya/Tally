using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tally.Domain.Entities;
using Tally.Domain.Matching;
using Tally.Infrastructure.Data;

namespace Tally.Infrastructure.Receipts;

public sealed record CoverLine(string Label, decimal Amount);
public sealed record ReviewItem(Guid ReceiptId, decimal? Amount, string? SenderName, string? Method, DateOnly? PaidOn,
                                bool Sure, string Headline, Guid? PayerId, string? Family, List<CoverLine> Covers);
public sealed record ReviewQueue(List<ReviewItem> Sure, List<ReviewItem> NeedsLook, decimal SureTotal);

public sealed record BulkApproveRequest(List<Guid> ReceiptIds);
public sealed record BulkItemResult(Guid ReceiptId, bool Approved, string Message);
public sealed record BulkApproveResult(int Approved, int Skipped, List<BulkItemResult> Items);

// Sorts waiting receipts into "sure" and "needs a look", and approves the sure ones in one go.
public sealed class ReviewService(TallyDbContext db, DetectiveService detective, ApprovalService approvals, ILogger<ReviewService> log)
{
    // Sure = read cleanly + known family + exactly one way the amount fits. Anything else: the owner looks.
    private static bool IsSure(Receipt r, DetectiveResult d) =>
        r.Status == ReceiptStatus.Read
        && r.Problem is null
        && d.Verdict == Verdict.Suggested
        && d.Best is { } f
        && f.Sender?.Confidence.ToString() is "Certain" or "Strong"
        && f.Coverage.Kind == CoverageKind.Exact
        && f.Coverage.Options.Count == 1;

    public async Task<ReviewQueue> GetQueueAsync(CancellationToken ct)
    {
        var receipts = await db.Receipts.AsNoTracking()
            .Where(r => r.Status == ReceiptStatus.Uploaded || r.Status == ReceiptStatus.Read || r.Status == ReceiptStatus.NeedsReview)
            .OrderBy(r => r.UploadedAtUtc)
            .ToListAsync(ct);

        var sure = new List<ReviewItem>();
        var look = new List<ReviewItem>();
        foreach (var r in receipts)
        {
            var d = await detective.InvestigateAsync(r.Id, ct);
            var ok = d is not null && IsSure(r, d);
            var f = d?.Best;
            var covers = ok
                ? f!.Coverage.Options[0].Lines.Select(l => new CoverLine(l.Label, l.Amount)).ToList()
                : [];
            var item = new ReviewItem(r.Id, r.Amount, r.SenderName, r.Method, r.PaidOn, ok,
                                      d?.Headline ?? "Not read yet", f?.PayerId, f?.Name, covers);
            (ok ? sure : look).Add(item);
        }
        return new ReviewQueue(sure, look, sure.Sum(i => i.Amount ?? 0));
    }

    public async Task<BulkApproveResult> ApproveSureAsync(IReadOnlyList<Guid> receiptIds, CancellationToken ct)
    {
        var items = new List<BulkItemResult>();
        foreach (var id in receiptIds.Distinct())
        {
            db.ChangeTracker.Clear();      // each receipt starts clean, so one failure can't leak into the next
            try
            {
                var r = await db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
                var d = r is null ? null : await detective.InvestigateAsync(id, ct);
                if (r is null || d is null || !IsSure(r, d))
                {
                    items.Add(new(id, false, "Changed since the list loaded. Open it to check."));
                    continue;
                }

                var f = d.Best!;
                var chargeIds = f.Coverage.Options[0].Lines.Select(l => l.ChargeId).ToList();
                var outcome = await approvals.ApproveAsync(id, new ApproveRequest(f.PayerId, chargeIds), ct);
                if (outcome.Result is null)
                {
                    items.Add(new(id, false, outcome.Problem ?? "Receipt not found."));
                    continue;
                }

                var noted = outcome.Result.PayPal.All(p => p.Noted);
                items.Add(new(id, true, $"{f.Name} · {Usd(outcome.Result.Amount)} recorded"
                    + (noted ? ", noted on PayPal" : ". PayPal note pending: open it and use Try PayPal again")));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Approve all: receipt {ReceiptId} failed", id);
                items.Add(new(id, false, "Something went wrong. Open it and approve it on its own."));
            }
        }
        db.ChangeTracker.Clear();
        return new BulkApproveResult(items.Count(i => i.Approved), items.Count(i => !i.Approved), items);
    }

    private static string Usd(decimal n) =>
        "$" + n.ToString(n % 1 == 0 ? "#,0" : "#,0.00", CultureInfo.InvariantCulture);
}