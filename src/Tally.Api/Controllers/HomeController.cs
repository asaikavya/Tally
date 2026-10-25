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
    // GET api/home?period=2026-11-01&asOf=2026-11-12  → the three numbers
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

        var bySource = await (
            from a in db.Allocations.AsNoTracking()
            join p in db.Payments on a.PaymentId equals p.Id
            join c in db.Charges on a.ChargeId equals c.Id
            where c.PeriodStart == period
            group a by p.Source == PaymentSource.PayPal into g
            select new { ViaPayPal = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var late = charges.Where(c => c.DueDate < today && c.AmountDue > c.AmountPaid).ToList();

        return new HomeSummaryDto(
            period,
            charges.Sum(c => c.AmountDue),
            bySource.Where(x => x.ViaPayPal).Sum(x => x.Total),
            bySource.Where(x => !x.ViaPayPal).Sum(x => x.Total),
            charges.Sum(c => c.AmountDue - c.AmountPaid),
            late.Sum(c => c.AmountDue - c.AmountPaid),
            late.Count);
    }

    // GET api/home/needs-you?period=2026-11-01&asOf=2026-11-12  → the cards
    [HttpGet("needs-you")]
    public async Task<ActionResult<IReadOnlyList<NeedsYouItemDto>>> NeedsYou(
        [FromQuery] DateOnly period,
        [FromQuery] DateOnly? asOf,
        CancellationToken ct)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await db.Charges.AsNoTracking()
            .Where(c => c.PeriodStart == period && c.DueDate < today && c.AmountDue > c.AmountPaid)
            .Select(c => new
            {
                PayerId = c.Enrollment!.Member!.PayerId,
                PayerName = c.Enrollment!.Member!.Payer!.Name,
                Country = c.Enrollment!.Member!.Country,
                Member = c.Enrollment!.Member!.Name,
                Offering = c.Enrollment!.PricePlan!.Offering!.Name,
                Owed = c.AmountDue - c.AmountPaid,
                c.DueDate
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.PayerId, r.PayerName, r.Country })
            .Select(g => new NeedsYouItemDto(
                "late",
                g.Key.PayerId,
                g.Key.PayerName,
                g.Key.Country,
                g.Sum(r => r.Owed),
                today.DayNumber - g.Min(r => r.DueDate).DayNumber,
                g.Select(r => $"{r.Member} · {r.Offering} ${r.Owed:0}").ToList()))
            .OrderByDescending(i => i.Amount)
            .ToList();
    }
}