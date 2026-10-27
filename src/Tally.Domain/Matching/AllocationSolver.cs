namespace Tally.Domain.Matching;

public enum CoverageKind { Exact, Close, Ambiguous, Overpaid, Partial, TooMany, NothingOpen }

public sealed record AllocationLine(Guid ChargeId, string Label, decimal ChargeRemaining, decimal Amount);
public sealed record AllocationOption(IReadOnlyList<AllocationLine> Lines, decimal Leftover, decimal StillDueAfter, string? Note);
public sealed record ComboTried(string Label, decimal Sum, bool Fits);
public sealed record Coverage(CoverageKind Kind, IReadOnlyList<AllocationOption> Options, IReadOnlyList<ComboTried> Tried,
                              int CombinationsChecked, IReadOnlyList<Evidence> Evidence);

public static class AllocationSolver
{
    public const int MaxCharges = 12;         // 2^12 = 4,096 combinations: instant. Beyond that, oldest-first.
    private const int ShowCombosUpTo = 4;     // 15 combinations fit on screen

    public static Coverage Solve(decimal amount, IReadOnlyList<OpenCharge> charges, string? memo = null)
    {
        var open = charges.Where(c => c.Remaining > 0)
    .OrderBy(c => c.DueDate).ThenBy(c => c.Student).ThenBy(c => c.Offering).ThenBy(c => c.ChargeId)
    .ToList();
        if (open.Count == 0)
            return new(CoverageKind.NothingOpen, [], [], 0, [new(Strength.Weak, "This family has nothing open")]);

        var total = open.Sum(c => c.Remaining);

        if (open.Count > MaxCharges)
            return new(CoverageKind.TooMany, [OldestFirst(amount, open, total)], [], 0,
                [new(Strength.Weak, $"{open.Count} open charges is too many to check every combination, so Tally filled the oldest first")]);

        if (amount > total)
        {
            var all = open.Select(c => Line(c, c.Remaining)).ToList();
            return new(CoverageKind.Overpaid, [new(all, amount - total, 0, $"{Text.Money(amount - total)} becomes family credit")], [], 0,
                [new(Strength.Medium, $"{Text.Money(amount)} is more than everything open ({Text.Money(total)})")]);
        }

        // Try every combination. Each bit of "mask" says whether one charge is in.
        var n = open.Count;
        var combos = new List<(int Mask, decimal Sum)>();
        for (var mask = 1; mask < (1 << n); mask++)
        {
            decimal sum = 0;
            for (var i = 0; i < n; i++) if ((mask >> i & 1) == 1) sum += open[i].Remaining;
            combos.Add((mask, sum));
        }
        var checkedCount = combos.Count;
        var exact = combos.Where(c => c.Sum == amount).Select(c => c.Mask).ToList();
        var tried = n <= ShowCombosUpTo
            ? combos.Select(c => new ComboTried(LabelOf(Pick(open, c.Mask), open), c.Sum, c.Sum == amount)).ToList()
            : [];

        if (exact.Count == 1)
        {
            var chosen = Pick(open, exact[0]);
            var ev = new List<Evidence> {
                new(Strength.Strong, $"Only {LabelOf(chosen, open)} adds up to exactly {Text.Money(amount)} (checked {checkedCount} {(checkedCount == 1 ? "combination" : "combinations") })") };
            if (MemoEvidence(memo, chosen, open) is { } m) ev.Add(m);
            return new(CoverageKind.Exact, [Full(chosen, total, amount)], tried, checkedCount, ev);
        }

        if (exact.Count > 1)
        {
            // Several fit: the owner decides. A memo that names a class moves that option to the top.
            var options = exact.Select(m => Pick(open, m))
                .OrderByDescending(cs => MemoEvidence(memo, cs, open)?.Strength == Strength.Medium)
                .Select(cs => Full(cs, total, amount)).ToList();
            return new(CoverageKind.Ambiguous, options, tried, checkedCount,
                [new(Strength.Weak, $"{exact.Count} different combinations add up to {Text.Money(amount)}. Please choose")]);
        }

        // Close: a fee or rounding (within 2%, at most $5).
        var tolerance = Math.Min(Math.Round(amount * 0.02m, 2), 5m);
        var close = combos.Where(c => Math.Abs(c.Sum - amount) <= tolerance)
                          .OrderBy(c => Math.Abs(c.Sum - amount)).ThenByDescending(c => PopCount(c.Mask)).ToList();
        if (close.Count > 0)
        {
            var best = close[0];
            var chosen = Pick(open, best.Mask);
            var diff = amount - best.Sum;
            var note = diff < 0 ? $"Received {Text.Money(-diff)} less than these charges" : $"Received {Text.Money(diff)} more than these charges";
            return new(CoverageKind.Close,
                [new(chosen.Select(c => Line(c, c.Remaining)).ToList(), Math.Max(diff, 0), total - amount + Math.Max(diff, 0), note)],
                tried, checkedCount,
                [new(Strength.Medium, $"{Text.Money(amount)} is within {Text.Money(Math.Abs(diff))} of {LabelOf(chosen, open)} ({Text.Money(best.Sum)}), maybe a fee or rounding")]);
        }

        return new(CoverageKind.Partial, [OldestFirst(amount, open, total)], tried, checkedCount,
            [new(Strength.Weak, $"No combination equals {Text.Money(amount)}, so Tally filled the oldest charges first. You can change it")]);
    }

    private static AllocationOption Full(List<OpenCharge> chosen, decimal total, decimal amount) =>
        new(chosen.Select(c => Line(c, c.Remaining)).ToList(), 0, total - amount, null);

    private static AllocationOption OldestFirst(decimal amount, List<OpenCharge> open, decimal total)
    {
        var lines = new List<AllocationLine>();
        var left = amount;
        foreach (var c in open)
        {
            if (left <= 0) break;
            var share = Math.Min(c.Remaining, left);
            lines.Add(Line(c, share));
            left -= share;
        }
        return new(lines, left, total - (amount - left), null);
    }

    private static AllocationLine Line(OpenCharge c, decimal amount) => new(c.ChargeId, $"{Text.First(c.Student)} · {c.Offering}", c.Remaining, amount);
    private static List<OpenCharge> Pick(List<OpenCharge> open, int mask) => open.Where((_, i) => (mask >> i & 1) == 1).ToList();
    private static int PopCount(int mask) => System.Numerics.BitOperations.PopCount((uint)mask);

    // "Dance + Guitar"; adds the child's name when two children take the same class.
    private static string LabelOf(IEnumerable<OpenCharge> chosen, IReadOnlyList<OpenCharge> all)
    {
        var dup = all.GroupBy(c => c.Offering).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        return string.Join(" + ", chosen.Select(c => dup.Contains(c.Offering) ? $"{c.Offering} ({Text.First(c.Student)})" : c.Offering));
    }

    private static Evidence? MemoEvidence(string? memo, IEnumerable<OpenCharge> chosen, IReadOnlyList<OpenCharge> all)
    {
        var m = Text.Normalize(memo);
        if (m.Length == 0) return null;
        var mentioned = all.Select(c => c.Offering).Distinct().Where(o => m.Contains(Text.Normalize(o))).ToList();
        if (mentioned.Count == 0) return null;
        var chosenOfferings = chosen.Select(c => c.Offering).ToHashSet();
        return mentioned.All(chosenOfferings.Contains)
            ? new(Strength.Medium, $"Memo says \"{memo}\", which matches {string.Join(", ", mentioned)}")
            : new(Strength.Weak, $"Memo mentions {string.Join(", ", mentioned)}, which isn't in this combination");
    }
}