using Microsoft.AspNetCore.Mvc;
using Tally.Infrastructure.Billing;

namespace Tally.Api.Controllers;

[ApiController]
[Route("api/payers/{payerId:guid}/invoices")]
public class InvoicesController(InvoicingService invoicing) : ControllerBase
{
    // POST /api/payers/{payerId}/invoices?period=2026-11-01
    [HttpPost]
    public async Task<ActionResult<BillingResult>> Create(Guid payerId, [FromQuery] DateOnly period, CancellationToken ct)
        => Ok(await invoicing.BillPayerAsync(payerId, period, ct));
}