using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
    public class Invoice : Entity
    {
        public Guid PayerId { get; set; }
        public DateOnly PeriodStart { get; set; }
        public required string PayPalInvoiceId { get; set; }
        public required string PayPalStatus { get; set; }
        public DateTime? SentAtUtc { get; set; }
    }
}
