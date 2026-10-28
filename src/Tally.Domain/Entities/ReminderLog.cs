namespace Tally.Domain.Entities;

// Every reminder that went out: so a family is never reminded twice in one day.
public class ReminderLog : Entity
{
    public Guid PayerId { get; set; }
    public Guid InvoiceId { get; set; }
    public required string Language { get; set; }
    public required string Subject { get; set; }
    public required string Note { get; set; }
    public DateTime SentAtUtc { get; set; }
}