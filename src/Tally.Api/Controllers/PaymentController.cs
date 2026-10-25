using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Contracts;
using Tally.Infrastructure.Data;

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
                c.DueDate
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

        // 3) Status is decided here, once, so every screen agrees.
        return charges
     .Select(c =>
     {
         var remaining = c.AmountDue - c.AmountPaid;
         var status = remaining <= 0 ? "Paid"
                    : c.AmountPaid > 0 ? "Partial"
                    : c.DueDate < today ? "Late"
                    : "Due";

         var inv = c.InvoiceId is Guid id && invoices.TryGetValue(id, out var found)
             ? found
             : ((string?)null, (string?)null);

         return new PaymentRowDto(
             c.Id, c.PayerId, c.Family, c.Student, c.Offering, c.Plan.ToString(),
             c.AmountDue, c.AmountPaid, remaining, c.DueDate, status,
             viaByCharge.GetValueOrDefault(c.Id, ""),
             inv.Item1, inv.Item2);
     })
     .OrderBy(r => r.Family).ThenBy(r => r.Student)
     .ToList();
    }
}