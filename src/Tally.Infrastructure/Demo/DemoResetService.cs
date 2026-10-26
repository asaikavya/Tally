using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tally.Domain.Entities;
using Tally.Infrastructure.Billing;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.Webhooks;

namespace Tally.Infrastructure.Demo;

public sealed record DemoResetResult(int Families, int PayPalBillsSent, IReadOnlyList<string> Warnings);

// Wipe → seed the story → link the really-paid sandbox invoices → sync from PayPal → send fresh bills.
public sealed class DemoResetService(
    TallyDbContext db,
    InvoicingService invoicing,
    PayPalEventHandler paypalEvents,
    IOptions<DemoOptions> options,
    ILogger<DemoResetService> log)
{
    public async Task<DemoResetResult> ResetAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var warnings = new List<string>();

        // 1) Wipe, children first (all foreign keys are Restrict), in one transaction.
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Allocations.ExecuteDeleteAsync(ct);
            await db.Payments.ExecuteDeleteAsync(ct);
            await db.Charges.ExecuteDeleteAsync(ct);
            await db.Invoices.ExecuteDeleteAsync(ct);
            await db.Enrollments.ExecuteDeleteAsync(ct);
            await db.PayerAliases.ExecuteDeleteAsync(ct);
            await db.Members.ExecuteDeleteAsync(ct);
            await db.Payers.ExecuteDeleteAsync(ct);
            await db.Groups.ExecuteDeleteAsync(ct);
            await db.PricePlans.ExecuteDeleteAsync(ct);
            await db.Offerings.ExecuteDeleteAsync(ct);
            await db.Locations.ExecuteDeleteAsync(ct);
            await db.Organizers.ExecuteDeleteAsync(ct);
            await db.WebhookEvents.ExecuteDeleteAsync(ct);
            await db.Receipts.ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
            
        }
        db.ChangeTracker.Clear();

        // 2) Seed the story.
        var payerIds = await DemoSeeder.SeedStoryAsync(db, opts.PayerEmail, ct);

        // 3) Link the sandbox invoices that were really paid, then let PayPal tell us they're PAID.
        await LinkPaidInvoiceAsync("Meera Patel", opts.MeeraPaidInvoiceId, payerIds, warnings, ct);
        await LinkPaidInvoiceAsync("Lakshmi Iyer", opts.LakshmiPaidInvoiceId, payerIds, warnings, ct);
        await paypalEvents.SyncOpenInvoicesAsync(ct);

        // 4) Fresh PayPal bills for the families who haven't paid yet.
        var sent = 0;
        foreach (var name in new[] { "Ravi Kumar", "Lucia Lopez", "Rohan Sharma" })
        {
            try
            {
                await invoicing.BillPayerAsync(payerIds[name], DemoSeeder.Nov, ct);
                sent++;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Demo reset could not bill {Payer}", name);
                warnings.Add($"Couldn't send {name}'s PayPal bill: {ex.Message}");
            }
        }

        log.LogInformation("Demo reset done: {Families} families, {Sent} PayPal bills sent", payerIds.Count, sent);
        return new DemoResetResult(payerIds.Count, sent, warnings);
    }

    private async Task LinkPaidInvoiceAsync(string payerName, string? payPalInvoiceId,
        Dictionary<string, Guid> payerIds, List<string> warnings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payPalInvoiceId))
        {
            warnings.Add($"No paid sandbox invoice configured for {payerName}.");
            return;
        }

        var payerId = payerIds[payerName];
        var charges = await db.Charges
            .Where(c => c.PeriodStart == DemoSeeder.Nov && c.Enrollment!.Member!.PayerId == payerId)
            .ToListAsync(ct);

        var invoice = new Invoice
        {
            PayerId = payerId,
            PeriodStart = DemoSeeder.Nov,
            PayPalInvoiceId = payPalInvoiceId,
            PayPalStatus = "SENT",            // sync replaces this with what PayPal really says
            SentAtUtc = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);
        foreach (var c in charges) c.InvoiceId = invoice.Id;
        await db.SaveChangesAsync(ct);
    }
}