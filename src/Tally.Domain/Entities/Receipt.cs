namespace Tally.Domain.Entities;

public enum ReceiptStatus { Uploaded, Read, NeedsReview, Duplicate, Matched, Rejected }

public class Receipt : Entity
{
    public DateTime UploadedAtUtc { get; set; }
    public required string UploadedBy { get; set; }      // "owner" or "parent"
    public Guid? PayerId { get; set; }                   // known when a parent uploads from their own link
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public required string ImageHash { get; set; }       // SHA-256: same image twice = duplicate
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Uploaded;

    // What we believe now (AI's reading, plus any owner corrections)
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? SenderName { get; set; }
    public string? PaidTo { get; set; }
    public string? Memo { get; set; }
    public string? Reference { get; set; }
    public string? Method { get; set; }

    public string? AiRaw { get; set; }                   // exactly what the AI returned, never overwritten
    public string? EditedFields { get; set; }            // e.g. "Amount,PaidOn": what the owner corrected
    public string? Problem { get; set; }                 // plain-English reason it needs a human
    public Guid? DuplicateOfId { get; set; }
}