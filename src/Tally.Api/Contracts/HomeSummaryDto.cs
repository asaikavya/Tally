namespace Tally.Api.Contracts;

// What the Home screen needs, already added up. React never does money math.
public sealed record HomeSummaryDto(
    DateOnly Period,
    decimal Billed,
    decimal PaidThroughPayPal,
    decimal RecordedOutsidePayPal,
    decimal StillDue,
    decimal Late,
    int LateCharges);