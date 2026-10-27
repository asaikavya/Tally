namespace Tally.Domain.Matching;

public enum Verdict { Suggested, ChooseFamily, ChooseClasses, NoMatch }

public sealed record FamilyFinding(SenderCandidate? Sender, Guid PayerId, string Name,
    IReadOnlyList<OpenCharge> OpenCharges, Coverage Coverage, IReadOnlyList<Evidence> Extra);

public sealed record DetectiveResult(Verdict Verdict, string Headline, FamilyFinding? Best,
    IReadOnlyList<FamilyFinding> Candidates, IReadOnlyList<string> RuledOut);
    

public static class PaymentDetective
{
    private static string Days(int n) => n == 1 ? "1 day" : $"{n} days";
    public static DetectiveResult Investigate(ReceiptFacts facts, IReadOnlyList<PayerInfo> payers,
        IReadOnlyList<OpenCharge> openCharges, IReadOnlyList<PastPayment> history)
    {
        if (facts.Amount <= 0) return new(Verdict.NoMatch, "Type the amount first", null, [], []);

        var byPayer = openCharges.GroupBy(c => c.PayerId).ToDictionary(g => g.Key, g => (IReadOnlyList<OpenCharge>)g.ToList());
        IReadOnlyList<OpenCharge> OpenOf(Guid id) => byPayer.TryGetValue(id, out var list) ? list : [];

        var senders = SenderMatcher.Find(facts, payers, history);
        var sure = senders.Where(s => s.Confidence is SenderConfidence.Certain or SenderConfidence.Strong).ToList();

        // One family is clearly the sender → work out what it covers.
        if (sure.Count == 1)
        {
            var s = sure[0];
            var open = OpenOf(s.PayerId);
            var cov = AllocationSolver.Solve(facts.Amount, open, facts.Memo);
            var finding = new FamilyFinding(s, s.PayerId, s.Name, open, cov, TimingEvidence(facts, cov, open));
            var verdict = cov.Kind switch
            {
                CoverageKind.Ambiguous or CoverageKind.TooMany => Verdict.ChooseClasses,
                CoverageKind.NothingOpen => Verdict.NoMatch,
                _ => Verdict.Suggested
            };
            return new(verdict, Headline(facts, s.Name, cov), finding, [], RuledOut(facts, payers, s.PayerId));
        }

        // Not sure who: try each named candidate, or every family with something open. The amount only SUGGESTS.
        var pool = senders.Count > 0 ? senders.Select(x => x.PayerId) : byPayer.Keys;
        var candidates = pool
            .Select(id => (Payer: payers.First(p => p.PayerId == id), Open: OpenOf(id)))
            .Select(x => new FamilyFinding(senders.FirstOrDefault(s => s.PayerId == x.Payer.PayerId), x.Payer.PayerId, x.Payer.Name,
                x.Open, AllocationSolver.Solve(facts.Amount, x.Open, facts.Memo), []))
            .Where(f => f.Coverage.Kind != CoverageKind.NothingOpen)
            .OrderBy(f => f.Coverage.Kind)                 // Exact first, then Close, Ambiguous...
            .ThenBy(f => f.Name)
            .ToList();
        // If some families fit, only offer those. The rest is noise.
        var fitting = candidates.Where(f => f.Coverage.Kind is CoverageKind.Exact or CoverageKind.Close or CoverageKind.Ambiguous).ToList();
        if (fitting.Count > 0) candidates = fitting;

        var exactFits = candidates.Count(f => f.Coverage.Kind == CoverageKind.Exact);
        var headline = exactFits == 1
            ? $"Who sent {Text.Money(facts.Amount)}? It fits {candidates[0].Name}'s charges exactly. Please confirm"
            : $"Who sent {Text.Money(facts.Amount)}? {(exactFits > 1 ? $"{exactFits} families' charges fit" : "No family's charges fit exactly")}";
        return new(candidates.Count == 0 ? Verdict.NoMatch : Verdict.ChooseFamily, headline, null, candidates, []);
    }

    private static string Headline(ReceiptFacts f, string name, Coverage cov) => cov.Kind switch
    {
        CoverageKind.Exact => $"Suggested match: {Text.Money(f.Amount)} from {name}",
        CoverageKind.Close => $"Close match: {Text.Money(f.Amount)} from {name}",
        CoverageKind.Overpaid => $"{Text.Money(f.Amount)} from {name} covers everything, with {Text.Money(cov.Options[0].Leftover)} extra",
        CoverageKind.Partial => $"Part payment: {Text.Money(f.Amount)} from {name}",
        CoverageKind.Ambiguous => $"{Text.Money(f.Amount)} from {name}: which classes?",
        CoverageKind.TooMany => $"{Text.Money(f.Amount)} from {name}: please check the classes",
        _ => $"{name} has nothing open"
    };

    private static IReadOnlyList<Evidence> TimingEvidence(ReceiptFacts f, Coverage cov, IReadOnlyList<OpenCharge> open)
    {
        if (f.PaidOn is not DateOnly paid || cov.Options.Count == 0) return [];
        var ids = cov.Options[0].Lines.Select(l => l.ChargeId).ToHashSet();
        var due = open.Where(c => ids.Contains(c.ChargeId)).Select(c => c.DueDate).DefaultIfEmpty().Min();
        if (due == default) return [];
        return paid <= due
            ? [new(Strength.Weak, $"Paid {Text.Day(paid)}, before it was due ({Text.Day(due)})")]
            : [new(Strength.Weak, $"Paid {Text.Day(paid)}, {Days(paid.DayNumber - due.DayNumber)} after it was due")];
    }

    // Families that a quick glance might confuse ("R..."), shown so the owner sees we checked.
    private static IReadOnlyList<string> RuledOut(ReceiptFacts f, IReadOnlyList<PayerInfo> payers, Guid chosen)
    {
        var s = Text.Normalize(f.SenderName);
        if (s.Length == 0) return [];
        return payers.Where(p => p.PayerId != chosen && Text.Normalize(p.Name).StartsWith(s[0]))
                     .Select(p => $"{p.Name}: also starts with \"{s[0]}\", but the surname doesn't match")
                     .ToList();
    }
}