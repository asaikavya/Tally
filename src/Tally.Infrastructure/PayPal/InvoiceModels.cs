namespace Tally.Infrastructure.PayPal;

public sealed record CreateInvoiceRequest(
    InvoiceDetail Detail, Invoicer Invoicer,
    IReadOnlyList<Recipient> PrimaryRecipients, IReadOnlyList<InvoiceItem> Items);

public sealed record InvoiceDetail(string CurrencyCode, string InvoiceDate, PaymentTerm PaymentTerm, string? Note);
public sealed record PaymentTerm(string DueDate);
public sealed record Invoicer(string BusinessName);
public sealed record Recipient(BillingInfo BillingInfo);
public sealed record BillingInfo(string EmailAddress, PersonName Name);
public sealed record PersonName(string GivenName);
public sealed record InvoiceItem(string Name, string Description, string Quantity, Money UnitAmount);
public sealed record Money(string CurrencyCode, string Value);

public sealed record SendInvoiceRequest(bool SendToRecipient, bool SendToInvoicer);

public sealed record InvoiceResponseDetail(InvoiceMetadata? Metadata);
public sealed record InvoiceMetadata(string? RecipientViewUrl);


public sealed record InvoiceResponse(string Id, string Status, InvoiceResponseDetail? Detail, InvoicePayments? Payments);
public sealed record InvoicePayments(Money? PaidAmount, IReadOnlyList<InvoiceTransaction>? Transactions);
public sealed record InvoiceTransaction(string? PaymentId, Money? Amount, string? PaymentDate, string? Method);

public sealed record WebhookHeaders(string AuthAlgo, string CertUrl, string TransmissionId, string TransmissionSig, string TransmissionTime);
public sealed record VerifyWebhookRequest(string AuthAlgo, string CertUrl, string TransmissionId,
    string TransmissionSig, string TransmissionTime, string WebhookId, System.Text.Json.JsonElement WebhookEvent);
public sealed record VerifyWebhookResponse(string VerificationStatus);