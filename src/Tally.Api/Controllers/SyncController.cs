using Microsoft.AspNetCore.Mvc;
using Tally.Infrastructure.Webhooks;

namespace Tally.Api.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController(PayPalEventHandler handler) : ControllerBase
{
    // POST /api/sync/paypal
    [HttpPost("paypal")]
    public async Task<IActionResult> SyncPayPal(CancellationToken ct)
        => Ok(new { invoicesChecked = await handler.SyncOpenInvoicesAsync(ct) });
}