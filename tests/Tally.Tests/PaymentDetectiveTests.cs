using Tally.Domain.Matching;

namespace Tally.Tests;

public class PaymentDetectiveTests
{
    private static readonly Guid Ravi = Guid.NewGuid(), Lucia = Guid.NewGuid(), Rohan = Guid.NewGuid();
    private static readonly DateOnly Nov10 = new(2026, 11, 10);

    private static OpenCharge C(Guid payer, string student, string offering, decimal amount, int dueDay = 10) =>
        new(Guid.CreateVersion7(), payer, student, offering, amount, new DateOnly(2026, 11, dueDay));

    private static List<OpenCharge> Kumar() =>
        [C(Ravi, "Ananya Kumar", "Piano", 100, 8), C(Ravi, "Ananya Kumar", "Dance", 90, 9), C(Ravi, "Arjun Kumar", "Guitar", 80, 10)];

    private static readonly List<PayerInfo> Payers =
    [
        new(Ravi, "Ravi Kumar", ["R KUMAR"]),
        new(Lucia, "Lucia Lopez", []),
        new(Rohan, "Rohan Sharma", []),
    ];

    private static ReceiptFacts Facts(decimal amount, string? sender = null, string? memo = null, Guid? link = null) =>
        new(amount, sender, memo, new DateOnly(2026, 11, 9), link);

    [Fact]
    public void Exact_single_fit_170_is_dance_plus_guitar()
    {
        var cov = AllocationSolver.Solve(170, Kumar());
        Assert.Equal(CoverageKind.Exact, cov.Kind);
        Assert.Equal(["Dance", "Guitar"], cov.Options[0].Lines.Select(l => l.Label.Split(" · ")[1]));
        Assert.Equal(7, cov.CombinationsChecked);
        Assert.Equal(100, cov.Options[0].StillDueAfter);
    }

    [Fact]
    public void Exact_180_is_piano_plus_guitar_not_credit()
    {
        var cov = AllocationSolver.Solve(180, Kumar());
        Assert.Equal(CoverageKind.Exact, cov.Kind);
        Assert.Equal(180, cov.Options[0].Lines.Sum(l => l.Amount));
    }

    [Fact]
    public void Two_exact_fits_means_owner_chooses()
    {
        var twins = new List<OpenCharge> { C(Ravi, "Ananya", "Dance", 90), C(Ravi, "Arjun", "Dance", 90) };
        var cov = AllocationSolver.Solve(90, twins);
        Assert.Equal(CoverageKind.Ambiguous, cov.Kind);
        Assert.Equal(2, cov.Options.Count);
    }

    [Fact]
    public void Memo_naming_a_class_moves_that_option_first()
    {
        var charges = new List<OpenCharge> { C(Ravi, "Ananya", "Art", 90), C(Ravi, "Ananya", "Dance", 90) };
        var cov = AllocationSolver.Solve(90, charges, memo: "dance nov");
        Assert.Equal(CoverageKind.Ambiguous, cov.Kind);
        Assert.Contains("Dance", cov.Options[0].Lines[0].Label);
    }

    [Fact]
    public void More_than_everything_open_becomes_credit()
    {
        var cov = AllocationSolver.Solve(300, Kumar());
        Assert.Equal(CoverageKind.Overpaid, cov.Kind);
        Assert.Equal(30, cov.Options[0].Leftover);
        Assert.Equal(0, cov.Options[0].StillDueAfter);
    }

    [Fact]
    public void No_fit_fills_oldest_first()
    {
        var cov = AllocationSolver.Solve(150, Kumar());
        Assert.Equal(CoverageKind.Partial, cov.Kind);
        Assert.Equal([100m, 50m], cov.Options[0].Lines.Select(l => l.Amount));   // piano full, $50 toward dance
        Assert.Equal(120, cov.Options[0].StillDueAfter);
    }

    [Fact]
    public void Two_dollars_short_is_a_close_match()
    {
        var cov = AllocationSolver.Solve(168, Kumar());
        Assert.Equal(CoverageKind.Close, cov.Kind);
        Assert.Contains("less", cov.Options[0].Note);
    }

    [Fact]
    public void Thirteen_charges_skips_the_search()
    {
        var many = Enumerable.Range(1, 13).Select(i => C(Ravi, "Kid", $"Class {i}", 10, i)).ToList();
        var cov = AllocationSolver.Solve(25, many);
        Assert.Equal(CoverageKind.TooMany, cov.Kind);
        Assert.Equal(0, cov.CombinationsChecked);
    }

    [Fact]
    public void Known_sender_name_gives_suggested_match_and_rules_out_rohan()
    {
        var result = PaymentDetective.Investigate(Facts(170, "R KUMAR"), Payers, Kumar(), []);
        Assert.Equal(Verdict.Suggested, result.Verdict);
        Assert.Equal(Ravi, result.Best!.PayerId);
        Assert.Contains(result.RuledOut, r => r.StartsWith("Rohan Sharma"));
    }

    [Fact]
    public void Upload_from_family_link_is_certain_even_without_a_name()
    {
        var charges = new List<OpenCharge> { C(Lucia, "Diego Lopez", "Dance", 90) };
        var result = PaymentDetective.Investigate(Facts(90, link: Lucia), Payers, charges, []);
        Assert.Equal(Verdict.Suggested, result.Verdict);
        Assert.Equal(SenderConfidence.Certain, result.Best!.Sender!.Confidence);
    }

    [Fact]
    public void No_name_and_two_families_fit_means_owner_chooses_the_family()
    {
        var charges = Kumar().Append(C(Lucia, "Diego Lopez", "Dance", 90)).ToList();
        var result = PaymentDetective.Investigate(Facts(90), Payers, charges, []);
        Assert.Equal(Verdict.ChooseFamily, result.Verdict);
        Assert.Null(result.Best);                           // never guesses
        Assert.Equal(2, result.Candidates.Count(c => c.Coverage.Kind == CoverageKind.Exact));
    }
}