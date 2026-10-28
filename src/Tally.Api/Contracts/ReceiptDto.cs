using System.Globalization;
using System.Text.Json;
using Tally.Domain.Entities;

namespace Tally.Api.Contracts;

// One field the owner corrected: what the AI read, and what it says now.
public sealed record Correction(string Field, string? AiValue, string? NowValue);

public sealed record ReceiptDto(
    Guid Id, string Status, string UploadedBy, DateTime UploadedAtUtc,
    decimal? Amount, string? Currency, DateOnly? PaidOn, string? SenderName, string? PaidTo,
    string? Memo, string? Reference, string? Method,
    string? Problem, Guid? DuplicateOfId, IReadOnlyList<string> Edited, bool AiRead,
    IReadOnlyList<Correction> Corrections)
{
    public static ReceiptDto From(Receipt r) => new(
        r.Id, r.Status.ToString(), r.UploadedBy, r.UploadedAtUtc,
        r.Amount, r.Currency, r.PaidOn, r.SenderName, r.PaidTo, r.Memo, r.Reference, r.Method,
        r.Problem, r.DuplicateOfId,
        EditedList(r), r.AiRaw is not null,
        CorrectionsOf(r));

    private static string[] EditedList(Receipt r) =>
        (r.EditedFields ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Compare the AI's original answer (AiRaw, never overwritten) with today's value, field by field.
    private static IReadOnlyList<Correction> CorrectionsOf(Receipt r)
    {
        var edited = EditedList(r);
        if (edited.Length == 0 || r.AiRaw is null) return [];

        JsonElement ai;
        try { using var doc = JsonDocument.Parse(r.AiRaw); ai = doc.RootElement.Clone(); }
        catch (JsonException) { return []; }

        return edited
            .Select(f => new Correction(f, AiValue(ai, f), NowValue(r, f)))
            .Where(c => !Same(c.Field, c.AiValue, c.NowValue))
            .ToList();
    }

    private static string? AiValue(JsonElement ai, string field)
    {
        if (ai.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in ai.EnumerateObject())
            if (string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => p.Value.GetString(),
                    _ => p.Value.GetRawText(),            // numbers, e.g. 107
                };
        return null;
    }

    private static string? NowValue(Receipt r, string field) => field switch
    {
        "Amount" => r.Amount?.ToString(CultureInfo.InvariantCulture),
        "Currency" => r.Currency,
        "PaidOn" => r.PaidOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "SenderName" => r.SenderName,
        "PaidTo" => r.PaidTo,
        "Memo" => r.Memo,
        "Reference" => r.Reference,
        "Method" => r.Method,
        _ => null,
    };

    // "170" and "170.00" are the same amount; text compares ignoring case and spaces.
    private static bool Same(string field, string? a, string? b) =>
        field == "Amount"
        && decimal.TryParse(a, NumberStyles.Number, CultureInfo.InvariantCulture, out var x)
        && decimal.TryParse(b, NumberStyles.Number, CultureInfo.InvariantCulture, out var y)
            ? x == y
            : string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}