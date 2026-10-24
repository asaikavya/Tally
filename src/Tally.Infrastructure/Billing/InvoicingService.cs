using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tally.Domain.Entities;
using Tally.Infrastructure.Data;
using Tally.Infrastructure.PayPal;

namespace Tally.Infrastructure.Billing;

public sealed record BillingResult(Guid InvoiceId, string PayPalInvoiceId, string? PayUrl);

public sealed class InvoicingService(TallyDbContext db, PayPalInvoiceClient paypal, ILogger<InvoicingService> log)
{
    public async Task<BillingResult> BillPayerAsync(Guid payerId, DateOnly period, CancellationToken ct)
    {
        // Already billed this family for this month? Return it instead of creating a duplicate.
        var existing = await db.Invoices.FirstOrDefaultAsync(i => i.PayerId == payerId && i.PeriodStart == period, ct);
        if (existing is not null)
        {
            if (existing.PayUrl is null)                       // backfill the link if we don't have it yet
            {
                var current = await paypal.GetAsync(existing.PayPalInvoiceId, ct);
                existing.PayUrl = current.Detail?.Metadata?.RecipientViewUrl;
                await db.SaveChangesAsync(ct);
            }
            return new BillingResult(existing.Id, existing.PayPalInvoiceId, existing.PayUrl);
        }

        var payer = await db.Payers.FindAsync([payerId], ct)
                    ?? throw new KeyNotFoundException($"Payer {payerId} not found.");
        if (string.IsNullOrWhiteSpace(payer.Email))
            throw new InvalidOperationException($"{payer.Name} has no email address.");

        var charges = await db.Charges
            .Include(c => c.Enrollment!).ThenInclude(e => e.Member)
            .Include(c => c.Enrollment!).ThenInclude(e => e.PricePlan)
            .Where(c => c.PeriodStart == period && c.InvoiceId == null && c.Enrollment!.Member!.PayerId == payerId)
            .ToListAsync(ct);
        if (charges.Count == 0) throw new InvalidOperationException("Nothing to bill for this period.");

        var organizer = await db.Organizers.FirstAsync(ct);          // one organizer for now
        var currency = organizer.DefaultCurrency;

        var request = new CreateInvoiceRequest(
            Detail: new InvoiceDetail(
                CurrencyCode: currency,
                InvoiceDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),   // today, never a future date
                PaymentTerm: new PaymentTerm(charges.Max(c => c.DueDate).ToString("yyyy-MM-dd")),
                Note: $"Fees for {period:MMMM yyyy}. Thank you!"),
            Invoicer: new Invoicer(organizer.Name),
            PrimaryRecipients: [new Recipient(new BillingInfo(payer.Email, new PersonName(payer.Name)))],
            Items: charges.Select(c => new InvoiceItem(
                Name: $"{c.Enrollment!.Member!.Name} – {c.Enrollment.PricePlan!.Name}",
                Description: period.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                Quantity: "1",
                UnitAmount: new Money(currency, c.AmountDue.ToString("0.00", CultureInfo.InvariantCulture))))
                .ToList());

        var draft = await paypal.CreateDraftAsync(request, ct);
        await paypal.SendInvoiceAsync(draft.Id, ct);
        var sent = await paypal.GetAsync(draft.Id, ct);              // fresh copy, includes the payer's link

        var payUrl = sent.Detail?.Metadata?.RecipientViewUrl;

        var invoice = new Invoice
        {
            PayerId = payerId,
            PeriodStart = period,
            PayPalInvoiceId = draft.Id,
            PayPalStatus = sent.Status,
            SentAtUtc = DateTime.UtcNow,
            PayUrl = payUrl
        };
        db.Invoices.Add(invoice);
        foreach (var charge in charges) charge.InvoiceId = invoice.Id;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Sent PayPal invoice {PayPalInvoiceId} to {Payer} for {Period}", draft.Id, payer.Name, period);
        return new BillingResult(invoice.Id, draft.Id, payUrl);
    }
}