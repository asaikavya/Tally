using Tally.Domain.Billing;
using Tally.Domain.Entities;

namespace Tally.Tests;

public class PaymentAllocatorTests
{
    private static Charge NewCharge(decimal due, int dueDay) =>
        new() { AmountDue = due, DueDate = new DateOnly(2026, 11, dueDay), PeriodStart = new DateOnly(2026, 11, 1) };

    [Fact]
    public void Full_payment_covers_every_charge()
    {
        var charges = new[] { NewCharge(100m, 10), NewCharge(90m, 10), NewCharge(80m, 10) };
        var result = PaymentAllocator.Allocate(charges, 270m);
        Assert.Equal(270m, result.Sum(r => r.Amount));
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Partial_payment_fills_earliest_due_first()
    {
        var later = NewCharge(100m, 20);
        var earlier = NewCharge(100m, 10);
        var result = PaymentAllocator.Allocate([later, earlier], 60m);
        Assert.Single(result);
        Assert.Same(earlier, result[0].Charge);
        Assert.Equal(60m, result[0].Amount);
    }
}