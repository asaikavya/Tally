using Tally.Domain.Entities;

namespace Tally.Api.Contracts;

public sealed record ReceiptDto(
    Guid Id, string Status, string UploadedBy, DateTime UploadedAtUtc,
    decimal? Amount, string? Currency, DateOnly? PaidOn, string? SenderName, string? PaidTo,
    string? Memo, string? Reference, string? Method,
    string? Problem, Guid? DuplicateOfId, IReadOnlyList<string> Edited,bool AiRead)

{
    public static ReceiptDto From(Receipt r) => new(
        r.Id, r.Status.ToString(), r.UploadedBy, r.UploadedAtUtc,
        r.Amount, r.Currency, r.PaidOn, r.SenderName, r.PaidTo, r.Memo, r.Reference, r.Method,
        r.Problem, r.DuplicateOfId,
        (r.EditedFields ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries), r.AiRaw is not null);
}