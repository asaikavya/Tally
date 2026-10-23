using System;
using System.Collections.Generic;
using System.Text;
using Tally.Domain.Entities;

namespace Tally.Domain.Entities
{
    public class Payment : Entity
    {
        public PaymentSource Source { get; set; }
        public decimal Amount { get; set; }
        public required string Currency { get; set; }
        public DateTime ReceivedAtUtc { get; set; }
        public string? SenderText { get; set; }                  // "R Kumar" as it appeared
        public string? ExternalId { get; set; }                  // PayPal or UPI reference
        public string? EvidenceUrl { get; set; }
    }
}
