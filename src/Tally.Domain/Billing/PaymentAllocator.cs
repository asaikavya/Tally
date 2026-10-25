using Tally.Domain.Entities;

namespace Tally.Domain.Billing;

public static class PaymentAllocator
{
    // Spreads a payment across charges, earliest due first, until the money runs out
    public static IReadOnlyList<(Charge Charge, decimal Amount)> Allocate(IEnumerable<Charge> charges, decimal amount)
    {
        var result = new List<(Charge, decimal)>();
        foreach (var charge in charges.Where(c => c.Remaining > 0).OrderBy(c => c.DueDate).ThenBy(c => c.Id))
        {
            if (amount <= 0) break;
            var share = Math.Min(charge.Remaining, amount);
            result.Add((charge, share));
            amount -= share;
        }
        return result;
    }
}