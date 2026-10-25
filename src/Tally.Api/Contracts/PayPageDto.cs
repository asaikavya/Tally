using System.ComponentModel.DataAnnotations;

namespace Tally.Api.Contracts;

public sealed record PayLineDto(string Student, string Offering, decimal AmountDue, decimal Remaining);

public sealed record PayPageDto(
    string Academy,
    string PayerName,
    DateOnly Period,
    IReadOnlyList<PayLineDto> Lines,
    decimal Total,
    decimal Remaining,
    DateOnly? DueDate,
    string? PayUrl,          // the PayPal invoice link
    string? PayPalStatus,
    bool ParentSaysPaid);

public sealed record AlreadyPaidRequest(
    [Required, AllowedValues("Zelle", "UPI", "Cash", "Check", "Other")] string Method,
    [MaxLength(200)] string? Note);