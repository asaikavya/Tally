using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Contracts;
using Tally.Domain.Matching;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Receipts;
using Tally.Infrastructure.Storage;

namespace Tally.Api.Controllers;

[ApiController]
[Route("api/receipts")]
public sealed class ReceiptsController(ReceiptIntakeService intake, TallyDbContext db, IReceiptStore store) : ControllerBase
{
    // POST api/receipts   (multipart form, field "file") — owner uploads
    [HttpPost]
    [RequestSizeLimit(ReceiptIntakeService.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<ReceiptDto>> Upload(IFormFile file, CancellationToken ct)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var receipt = await intake.UploadAsync(stream, file.ContentType, "owner", null, ct);
            return ReceiptDto.From(receipt);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptDto>> Get(Guid id, CancellationToken ct)
    {
        var r = await db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? NotFound() : ReceiptDto.From(r);
    }

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> Image(Guid id, CancellationToken ct)
    {
        var r = await db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? NotFound() : File(store.OpenRead(r.StorageKey), r.ContentType);
    }

    // PUT api/receipts/{id} — the owner fixes a misread field (the pencil)
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReceiptDto>> Correct(Guid id, ReceiptCorrection body, CancellationToken ct)
    {
        var r = await intake.CorrectAsync(id, body, ct);
        return r is null ? NotFound() : ReceiptDto.From(r);
    }
    // GET api/receipts/{id}/detective
    [HttpGet("{id:guid}/detective")]
    public async Task<ActionResult<DetectiveResult>> Detective(Guid id, [FromServices] DetectiveService detective, CancellationToken ct)
    {
        var result = await detective.InvestigateAsync(id, ct);
        return result is null ? NotFound() : result;
    }
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ApproveRequest req,
    [FromServices] ApprovalService approvals, CancellationToken ct)
    => ToResult(await approvals.ApproveAsync(id, req, ct));

    [HttpPost("{id:guid}/undo")]
    public async Task<IActionResult> Undo(Guid id,
        [FromServices] ApprovalService approvals, CancellationToken ct)
        => ToResult(await approvals.UndoAsync(id, ct));
    [HttpGet("{id:guid}/approval")]
    public async Task<IActionResult> Approval(Guid id, [FromServices] ApprovalService approvals, CancellationToken ct)
    => await approvals.GetAsync(id, ct) is { } r ? Ok(r) : NotFound();
    private IActionResult ToResult(ApprovalOutcome o) =>
        !o.Found ? NotFound()
        : o.Problem is not null ? Conflict(new { problem = o.Problem })
        : Ok(o.Result);
    // POST api/receipts/{id}/read-again
    [HttpPost("{id:guid}/read-again")]
    public async Task<ActionResult<ReceiptDto>> ReadAgain(Guid id, CancellationToken ct)
    {
        var r = await intake.ReadAgainAsync(id, ct);
        return r is null ? NotFound() : ReceiptDto.From(r);
    }
    [HttpPost("{id:guid}/paypal-note")]
    public async Task<IActionResult> RetryPayPal(Guid id, [FromServices] ApprovalService approvals, CancellationToken ct)
    => ToResult(await approvals.RetryPayPalAsync(id, ct));

}