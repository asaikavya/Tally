namespace Tally.Domain.Entities;

// One "paid outside PayPal" note Tally recorded on a PayPal invoice. Kept so Undo can remove it.
public class PayPalNote : Entity
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public required string PayPalInvoiceId { get; set; }
    public required string PayPalPaymentId { get; set; }     // PayPal's id for the recorded payment
    public decimal Amount { get; set; }
    public required string StatusBefore { get; set; }        // e.g. SENT
    public required string StatusAfter { get; set; }         // e.g. PARTIALLY_PAID (read back from PayPal)
    public DateTime RecordedAtUtc { get; set; }
}