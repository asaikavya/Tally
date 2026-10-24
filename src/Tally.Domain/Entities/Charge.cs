using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
    public class Charge : Entity                                // one bill line: Ananya, Piano, November
    {
        public Guid EnrollmentId { get; set; }
        public Enrollment? Enrollment { get; set; }
        public Guid? InvoiceId { get; set; }                     // the family invoice it belongs to
        public DateOnly PeriodStart { get; set; }                // 2026-11-01 = November
        public DateOnly DueDate { get; set; }
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; private set; }
        public ChargeStatus Status { get; private set; } = ChargeStatus.Pending;
        public byte[] RowVersion { get; set; } = [];

        public decimal Remaining => AmountDue - AmountPaid;

        public void ApplyPayment(decimal amount)                 // the ONLY way to record money
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            AmountPaid += amount;
            Status = AmountPaid >= AmountDue ? ChargeStatus.Paid : ChargeStatus.Partial;
        }
    }
}
