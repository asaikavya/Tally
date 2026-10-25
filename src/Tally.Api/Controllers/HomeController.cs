using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Contracts;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;

namespace Tally.Api.Controllers;

[ApiController]
[Route("api/home")]
public sealed class HomeController(TallyDbContext db) : ControllerBase
{
    // GET api/home?period=2026-11-01&asOf=2026-11-12
    // asOf lets the demo pretend "today" is Nov 12; normally it's today's date.
    [HttpGet]
    public async Task<ActionResult<HomeSummaryDto>> Get(
        [FromQuery] DateOnly period,
        [FromQuery] DateOnly? asOf,
        CancellationToken ct)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var charges = await db.Charges.AsNoTracking()
     .Where(c => c.PeriodStart == period)
     .Select(c => new { c.AmountDue, c.AmountPaid, c.DueDate })
     .ToListAsync(ct);

        // Split what was paid by where the money came from.
        var bySource = await (
            from a in db.Allocations.AsNoTracking()
            join p in db.Payments on a.PaymentId equals p.Id
            join c in db.Charges on a.ChargeId equals c.Id
            where c.PeriodStart == period
            group a by p.Source == PaymentSource.PayPal into g
            select new { ViaPayPal = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var billed = charges.Sum(c => c.AmountDue);
        var stillDue = charges.Sum(c => c.AmountDue - c.AmountPaid);
        var late = charges.Where(c => c.DueDate < today && c.AmountDue > c.AmountPaid).ToList();

        return new HomeSummaryDto(
            period,
            billed,
            bySource.Where(x => x.ViaPayPal).Sum(x => x.Total),
            bySource.Where(x => !x.ViaPayPal).Sum(x => x.Total),
            stillDue,
            late.Sum(c => c.AmountDue - c.AmountPaid),
            late.Count);
    }
}