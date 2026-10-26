using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Contracts;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Receipts;

namespace Tally.Api.Controllers;

// Public pages for families. The token in the URL is the only key, so every query filters by it.
[ApiController]
[Route("api/pay/{token}")]
public sealed class PayController(TallyDbContext db, ReceiptIntakeService intake) : ControllerBase
{
    // GET api/pay/{token}?period=2026-11-01
    [HttpGet]
    public async Task<ActionResult<PayPageDto>> Get(string token, [FromQuery] DateOnly period, CancellationToken ct)
    {
        var payer = await db.Payers.AsNoTracking()
            .Where(p => p.PayToken == token)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(ct);
        if (payer is null) return NotFound();   // wrong link: say nothing else

        var lines = await db.Charges.AsNoTracking()
            .Where(c => c.PeriodStart == period && c.Enrollment!.Member!.PayerId == payer.Id)
            .Select(c => new
            {
                Student = c.Enrollment!.Member!.Name,
                Offering = c.Enrollment!.PricePlan!.Offering!.Name,
                c.AmountDue,
                c.AmountPaid,
                c.DueDate,
                c.ParentSaysPaidAtUtc
            })
            .ToListAsync(ct);

        var invoice = await db.Invoices.AsNoTracking()
            .Where(i => i.PayerId == payer.Id && i.PeriodStart == period)
            .Select(i => new { i.PayUrl, i.PayPalStatus })
            .FirstOrDefaultAsync(ct);

        var academy = await db.Organizers.AsNoTracking().Select(o => o.Name).FirstOrDefaultAsync(ct) ?? "";

        return new PayPageDto(
            academy,
            payer.Name,
            period,
            lines.Select(l => new PayLineDto(l.Student, l.Offering, l.AmountDue, l.AmountDue - l.AmountPaid)).ToList(),
            lines.Sum(l => l.AmountDue),
            lines.Sum(l => l.AmountDue - l.AmountPaid),
            lines.Count == 0 ? null : lines.Min(l => l.DueDate),
            invoice?.PayUrl,
            invoice?.PayPalStatus,
            lines.Any(l => l.ParentSaysPaidAtUtc != null && l.AmountDue > l.AmountPaid));
    }

    // POST api/pay/{token}/already-paid?period=2026-11-01   { "method": "Zelle", "note": "sent on Nov 9" }
    [HttpPost("already-paid")]
    public async Task<IActionResult> AlreadyPaid(
        string token, [FromQuery] DateOnly period, AlreadyPaidRequest body, CancellationToken ct)
    {
        var payerId = await db.Payers
            .Where(p => p.PayToken == token)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (payerId is null) return NotFound();

        var open = await db.Charges
            .Where(c => c.PeriodStart == period && c.Enrollment!.Member!.PayerId == payerId && c.AmountDue > c.AmountPaid)
            .ToListAsync(ct);

        var note = string.IsNullOrWhiteSpace(body.Note) ? body.Method : $"{body.Method}: {body.Note.Trim()}";
        foreach (var charge in open) charge.MarkParentSaysPaid(DateTime.UtcNow, note);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }
    // POST api/pay/{token}/receipt?period=2026-11-01  (multipart, field "file")
    [HttpPost("receipt")]
    [RequestSizeLimit(ReceiptIntakeService.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> UploadReceipt(string token, [FromQuery] DateOnly period, IFormFile file, CancellationToken ct)
    {
        var payerId = await db.Payers.Where(p => p.PayToken == token).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        if (payerId is null) return NotFound();

        try
        {
            await using var stream = file.OpenReadStream();
            await intake.UploadAsync(stream, file.ContentType, "parent", payerId, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // A receipt from the family's own link also counts as "I already paid".
        var open = await db.Charges
            .Where(c => c.PeriodStart == period && c.Enrollment!.Member!.PayerId == payerId && c.AmountDue > c.AmountPaid)
            .ToListAsync(ct);
        foreach (var charge in open) charge.MarkParentSaysPaid(DateTime.UtcNow, "Uploaded a receipt");
        await db.SaveChangesAsync(ct);

        return Accepted();   // parents don't see AI details, just "thanks"
    }
}