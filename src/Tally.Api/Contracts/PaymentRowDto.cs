namespace Tally.Api.Contracts;

// One row per charge. The grid groups these by family itself.
public sealed record PaymentRowDto(
    Guid ChargeId,
    Guid PayerId,
    string Family,
    string Student,
    string Offering,
    string Plan,          // "Monthly" / "Yearly"
    decimal AmountDue,
    decimal AmountPaid,
    decimal Remaining,
    DateOnly DueDate,
    string Status,        // Paid / Partial / Late / Due
    string PaidVia,
    string? PayPalInvoiceId,   // e.g. "INV2-26PV-HP78-UH2P-6WL9"; null if not billed yet
    string? PayPalStatus,
    decimal FamilyCredit);      // "PayPal", "Zelle", "PayPal + Zelle", or ""