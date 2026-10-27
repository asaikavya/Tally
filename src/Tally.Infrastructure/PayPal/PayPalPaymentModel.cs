namespace Tally.Infrastructure.PayPal;

// Sent to POST /v2/invoicing/invoices/{id}/payments (your PayPalJson turns these into snake_case)
public sealed record RecordPaymentRequest(string Method, string PaymentDate, RecordedAmount Amount, string Note);
public sealed record RecordedAmount(string CurrencyCode, string Value);
public sealed record RecordPaymentResponse(string PaymentId);