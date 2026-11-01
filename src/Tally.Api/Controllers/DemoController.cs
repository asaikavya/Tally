using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tally.Infrastructure.Demo;
using Tally.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;

namespace Tally.Api.Controllers;


[ApiController]
[Route("api/demo")]
public sealed class DemoController(DemoResetService reset, IOptions<DemoOptions> options) : ControllerBase
{
    private static readonly SemaphoreSlim Gate = new(1, 1);   // one reset at a time

    // POST api/demo/reset
    [HttpPost("reset")]
    public async Task<ActionResult<DemoResetResult>> Reset()
    {
        if (!options.Value.Enabled) return NotFound();
        if (!await Gate.WaitAsync(0)) return Conflict(new { message = "A reset is already running." });
        try
        {
            // CancellationToken.None: once started, finish — a half-reset is worse than a slow one.
            return await reset.ResetAsync(CancellationToken.None);
        }
        finally
        {
            Gate.Release();
        }
    }
    [HttpPost("sync")]       // ← new one goes here, inside the class
    public async Task<IActionResult> Sync([FromServices] PayPalEventHandler handler, CancellationToken ct)
       => Ok(new { checkedInvoices = await handler.SyncOpenInvoicesAsync(ct) });
    // GET api/demo/progress: which story steps are done since the last reset. Read from real data, so steps
    // tick however the visitor did them (any page, any tab).

}