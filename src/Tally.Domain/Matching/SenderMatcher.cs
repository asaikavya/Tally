namespace Tally.Domain.Matching;

public enum SenderConfidence { Certain, Strong, Possible }
public sealed record SenderCandidate(Guid PayerId, string Name, SenderConfidence Confidence, IReadOnlyList<Evidence> Evidence);

public static class SenderMatcher
{
    public static IReadOnlyList<SenderCandidate> Find(ReceiptFacts facts, IReadOnlyList<PayerInfo> payers, IReadOnlyList<PastPayment> history)
    {
        // Strongest clue: it came from the family's own private link.
        if (facts.FromFamilyLink is Guid linked && payers.FirstOrDefault(p => p.PayerId == linked) is { } owner)
            return [new(owner.PayerId, owner.Name, SenderConfidence.Certain,
                        [new(Strength.Strong, "Uploaded from the family's own pay link")])];

        var sender = Text.Normalize(facts.SenderName);
        if (sender.Length == 0) return [];                       // no name on the receipt: the amount can only suggest

        var found = new List<(PayerInfo Payer, List<Evidence> Evidence)>();
        foreach (var p in payers)
        {
            var ev = new List<Evidence>();
            if (p.Aliases.Any(a => Text.Normalize(a) == sender))
                ev.Add(new(Strength.Strong, $"\"{facts.SenderName}\" is a name {Text.First(p.Name)} has paid from before"));
            else if (Text.Normalize(p.Name) == sender)
                ev.Add(new(Strength.Strong, $"Sender name matches {p.Name}"));
            else if (SurnameMatches(sender, p.Name, out var initialToo))
                ev.Add(initialToo
                    ? new(Strength.Medium, $"\"{facts.SenderName}\" fits {p.Name} (initial and surname)")
                    : new(Strength.Weak, $"Surname matches {p.Name}"));
            if (ev.Count == 0) continue;

            var past = history
                .Where(h => h.PayerId == p.PayerId && Text.Normalize(h.SenderText) == sender)
                .OrderByDescending(h => h.ReceivedOn)
                .FirstOrDefault();
            if (past is not null)
                ev.Add(new(Strength.Medium, $"Paid {Text.Money(past.Amount)} as \"{past.SenderText}\" on {Text.Day(past.ReceivedOn)}"));

            found.Add((p, ev));
        }

        if (found.Count == 1)
            found[0].Evidence.Add(new(Strength.Weak, "No other family matches this name"));

        var strongCount = found.Count(f => IsStrong(f.Evidence, found.Count));
        return found.Select(f => new SenderCandidate(
                f.Payer.PayerId, f.Payer.Name,
                IsStrong(f.Evidence, found.Count) && strongCount == 1 ? SenderConfidence.Strong : SenderConfidence.Possible,
                f.Evidence))
            .ToList();
    }

    // Strong evidence, or medium evidence when this is the only family that fits the name.
    private static bool IsStrong(List<Evidence> ev, int candidates) =>
        ev.Any(e => e.Strength == Strength.Strong) || (candidates == 1 && ev.Any(e => e.Strength == Strength.Medium));

    private static bool SurnameMatches(string sender, string payerName, out bool initialToo)
    {
        var s = sender.Split(' ');
        var p = Text.Normalize(payerName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        initialToo = false;
        if (s.Length == 0 || p.Length < 2 || s[^1].Length < 2 || s[^1] != p[^1]) return false;
        initialToo = s.Length >= 2 && s[0][0] == p[0][0];
        return true;
    }
}