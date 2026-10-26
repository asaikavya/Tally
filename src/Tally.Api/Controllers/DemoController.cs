using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tally.Infrastructure.Demo;

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
}