using Microsoft.AspNetCore.Mvc;
using Tally.Infrastructure.Messages;

namespace Tally.Api.Controllers;

[ApiController]
[Route("api/reminders")]
public sealed class RemindersController(ReminderService reminders) : ControllerBase
{
    // POST api/reminders/draft?asOf=2026-11-12  { payerId, language? }
    [HttpPost("draft")]
    public async Task<IActionResult> Draft(DraftRequest req, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => await reminders.DraftAsync(req, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow), ct) is { } d ? Ok(d) : NotFound();

    // POST api/reminders/send  { payerId, subject, note, language }
    [HttpPost("send")]
    public async Task<IActionResult> Send(SendReminderRequest req, CancellationToken ct)
    {
        var (result, problem) = await reminders.SendAsync(req, ct);
        return problem is not null ? Conflict(new { problem }) : Ok(result);
    }
}