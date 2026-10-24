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

public sealed record InvoiceResponse(string Id, string Status, InvoiceResponseDetail? Detail);
public sealed record InvoiceResponseDetail(InvoiceMetadata? Metadata);
public sealed record InvoiceMetadata(string? RecipientViewUrl);