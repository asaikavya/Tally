using System.Globalization;

namespace Tally.Domain.Matching;

public enum Strength { Strong, Medium, Weak }
public sealed record Evidence(Strength Strength, string Text);

public sealed record OpenCharge(Guid ChargeId, Guid PayerId, string Student, string Offering, decimal Remaining, DateOnly DueDate);
public sealed record PayerInfo(Guid PayerId, string Name, IReadOnlyList<string> Aliases);
public sealed record PastPayment(Guid PayerId, string? SenderText, decimal Amount, DateOnly ReceivedOn);

// What we know about one receipt. FromFamilyLink = the payer whose private link it was uploaded from.
public sealed record ReceiptFacts(decimal Amount, string? SenderName, string? Memo, DateOnly? PaidOn, Guid? FromFamilyLink);

internal static class Text
{
    public static string Money(decimal d) => "$" + d.ToString("#,0.##", CultureInfo.InvariantCulture);
    public static string Day(DateOnly d) => d.ToString("MMM d", CultureInfo.InvariantCulture);
    public static string First(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;

    // "R. Kumar " → "R KUMAR": upper case, letters/digits only, single spaces.
    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var chars = s.ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}