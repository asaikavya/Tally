namespace Tally.Api.Contracts;

// One card in "Needs you". Kind lets us add "check-receipt" cards later (step 10).
public sealed record NeedsYouItemDto(
    string Kind,              // "late" for now
    Guid PayerId,
    string PayerName,
    string? Country,
    decimal Amount,
    int DaysLate,
    IReadOnlyList<string> Lines,
    string? Note,
    string? PayPalInvoiceId,     // the family's real PayPal bill for this month
    string? PayPalStatus);       // PayPal's own status: SENT, PARTIALLY_PAID, PAID…);   // e.g. "Diego · Dance $90"