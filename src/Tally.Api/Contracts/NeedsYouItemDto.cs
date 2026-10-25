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
    string? Note);   // e.g. "Diego · Dance $90"