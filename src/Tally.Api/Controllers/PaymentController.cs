using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Contracts;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Receipts;
using Tally.Infrastructure.Payments;

namespace Tally.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(TallyDbContext db) : ControllerBase
{
    // GET api/payments?period=2026-11-01&asOf=2026-11-12
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentRowDto>>> Get(
        [FromQuery] DateOnly period,
        [FromQuery] DateOnly? asOf,
        CancellationToken ct)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // 1) The charges, with the names the grid shows.
        var charges = await db.Charges.AsNoTracking()
            .Where(c => c.PeriodStart == period)
            .Select(c => new
            {
                c.Id,
                c.InvoiceId,
                PayerId = c.Enrollment!.Member!.PayerId,
                Family = c.Enrollment!.Member!.Payer!.Name,
                Student = c.Enrollment!.Member!.Name,
                Offering = c.Enrollment!.PricePlan!.Offering!.Name,
                Plan = c.Enrollment!.PricePlan!.Frequency,
                c.AmountDue,
                c.AmountPaid,
                c.DueDate,
                c.ParentSaysPaidAtUtc
            })
            .ToListAsync(ct);

        // 2) How each charge was paid (one charge can be paid by several payments).
        var ids = charges.Select(c => c.Id).ToList();
        var sources = await (
            from a in db.Allocations.AsNoTracking()
            join p in db.Payments on a.PaymentId equals p.Id
            where ids.Contains(a.ChargeId)
            select new { a.ChargeId, p.Source })
            .ToListAsync(ct);

        var viaByCharge = sources
            .GroupBy(s => s.ChargeId)
            .ToDictionary(g => g.Key, g => string.Join(" + ", g.Select(s => s.Source.ToString()).Distinct()));

        var invoiceIds = charges.Where(c => c.InvoiceId != null).Select(c => c.InvoiceId!.Value).Distinct().ToList();
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => (i.PayPalInvoiceId, i.PayPalStatus), ct);
        // 4) Family credit: money a family sent beyond what it was applied to (e.g. $300 for $270 → $30).
        var payerIds = charges.Select(c => c.PayerId).Distinct().ToList();
        var leftovers = await db.Payments.AsNoTracking()
            .Where(p => p.PayerId != null && payerIds.Contains(p.PayerId.Value))
            .Select(p => new
            {
                PayerId = p.PayerId!.Value,
                Left = p.Amount - (db.Allocations.Where(a => a.PaymentId == p.Id).Sum(a => (decimal?)a.Amount) ?? 0m)
            })
            .ToListAsync(ct);
        var creditByPayer = leftovers.Where(x => x.Left > 0)
            .GroupBy(x => x.PayerId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Left));

        // 3) Status is decided here, once, so every screen agrees.
        return charges
     .Select(c =>
     {
         var remaining = c.AmountDue - c.AmountPaid;
         var status = remaining <= 0 ? "Paid"
               : c.AmountPaid > 0 ? "Partial"
               : c.ParentSaysPaidAtUtc != null ? "Says paid"
               : c.DueDate < today ? "Late"
               : "Due";

         var inv = c.InvoiceId is Guid id && invoices.TryGetValue(id, out var found)
             ? found
             : ((string?)null, (string?)null);

         return new PaymentRowDto(
             c.Id, c.PayerId, c.Family, c.Student, c.Offering, c.Plan.ToString(),
             c.AmountDue, c.AmountPaid, remaining, c.DueDate, status,
             viaByCharge.GetValueOrDefault(c.Id, ""),
             inv.Item1, inv.Item2, creditByPayer.GetValueOrDefault(c.PayerId));
     })
     .OrderBy(r => r.Family).ThenBy(r => r.Student)
     .ToList();
    }
    [HttpPost("manual")]
    public async Task<IActionResult> RecordManual(ManualPaymentRequest req,
    [FromServices] ManualPaymentService manual, CancellationToken ct)
    => ToResult(await manual.RecordAsync(req, ct));

    [HttpPost("{id:guid}/undo")]
    public async Task<IActionResult> UndoManual(Guid id,
        [FromServices] ManualPaymentService manual, CancellationToken ct)
        => ToResult(await manual.UndoAsync(id, ct));

    private IActionResult ToResult(ApprovalOutcome o) =>
        !o.Found ? NotFound()
        : o.Problem is not null ? Conflict(new { problem = o.Problem })
        : Ok(o.Result);
}