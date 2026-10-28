namespace Tally.Domain.Messages;

// Only what one reminder needs: first names, classes, amounts. No emails, no other families.
public sealed record ReminderLine(string Student, string Offering, decimal Amount);
public sealed record ReminderFacts(string ParentFirstName, string AcademyName, IReadOnlyList<ReminderLine> Lines,
                                   decimal Total, int DaysLate, string Language);
public sealed record ReminderDraft(string Subject, string Note);

public interface IReminderWriter
{
    Task<ReminderDraft> DraftAsync(ReminderFacts facts, CancellationToken ct);
}