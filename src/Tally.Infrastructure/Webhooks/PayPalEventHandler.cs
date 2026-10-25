using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tally.Domain.Billing;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.PayPal;

namespace Tally.Infrastructure.Webhooks;

public sealed class PayPalEventHandler(TallyDbContext db, PayPalInvoiceClient paypal, ILogger<PayPalEventHandler> log)
{
    public async Task HandleAsync(Guid webhookEventId, CancellationToken ct)
    {
        var evt = await db.WebhookEvents.FindAsync([webhookEventId], ct);
        if (evt is null || evt.ProcessedAtUtc is not null) return;           // already handled

        if (evt.EventType == "INVOICING.INVOICE.PAID")
        {
            var paypalInvoiceId = ReadInvoiceId(evt.Payload);
            var invoice = paypalInvoiceId is null ? null
                : await db.Invoices.FirstOrDefaultAsync(i => i.PayPalInvoiceId == paypalInvoiceId, ct);

            if (invoice is null)
                log.LogWarning("Webhook {EventId}: invoice {InvoiceId} is not one of ours", evt.PayPalEventId, paypalInvoiceId);
            else
                await RecordPayPalPaymentsAsync(invoice, ct);
        }
        // Cancelled and refunded events are handled on Oct 27

        evt.ProcessedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);                                      // payments + charges + event, saved together
    }

    private async Task RecordPayPalPaymentsAsync(Invoice invoice, CancellationToken ct)
    {
        var current = await paypal.GetAsync(invoice.PayPalInvoiceId, ct);   // PayPal is the source of truth
        invoice.PayPalStatus = current.Status;

        var charges = await db.Charges.Where(c => c.InvoiceId == invoice.Id).ToListAsync(ct);

        foreach (var tx in current.Payments?.Transactions ?? [])
        {
            if (tx.PaymentId is null || tx.Amount is null) continue;
            if (await db.Payments.AnyAsync(p => p.ExternalId == tx.PaymentId, ct)) continue;   // already recorded

            var amount = decimal.Parse(tx.Amount.Value, CultureInfo.InvariantCulture);
            var payment = new Payment
            {
                Source = PaymentSource.PayPal,
                Amount = amount,
                Currency = tx.Amount.CurrencyCode,
                ReceivedAtUtc = DateTime.UtcNow,
                ExternalId = tx.PaymentId
            };
            db.Payments.Add(payment);

            foreach (var (charge, share) in PaymentAllocator.Allocate(charges, amount))
            {
                charge.ApplyPayment(share);
                db.Allocations.Add(new Allocation
                {
                    PaymentId = payment.Id,
                    ChargeId = charge.Id,
                    Amount = share,
                    MatchedBy = MatchedBy.Webhook,
                    Confidence = 1m,
                    Reasons = $"Paid through PayPal invoice {invoice.PayPalInvoiceId}"
                });
            }
            log.LogInformation("Recorded PayPal payment {PaymentId} of {Amount} for invoice {InvoiceId}",
                               tx.PaymentId, amount, invoice.PayPalInvoiceId);
        }
    }

    // The invoice id may sit at resource.invoice.id or resource.id, so check both
    private static string? ReadInvoiceId(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("resource", out var resource)) return null;
        if (resource.TryGetProperty("invoice", out var inv) && inv.TryGetProperty("id", out var nested)) return nested.GetString();
        return resource.TryGetProperty("id", out var id) ? id.GetString() : null;
    }
    // Reconciliation: ask PayPal directly about every invoice that isn't settled yet
    public async Task<int> SyncOpenInvoicesAsync(CancellationToken ct)
    {
        string[] settled = ["PAID", "MARKED_AS_PAID", "CANCELLED", "REFUNDED"];
        var open = await db.Invoices.Where(i => !settled.Contains(i.PayPalStatus)).ToListAsync(ct);

        foreach (var invoice in open)
            await RecordPayPalPaymentsAsync(invoice, ct);       // same logic the webhook uses

        await db.SaveChangesAsync(ct);
        return open.Count;
    }
}