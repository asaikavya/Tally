using System;
using System.Collections.Generic;
using System.Text;
using Tally.Domain.Entities;

namespace Tally.Domain.Entities
{
    public class PayerAlias : Entity                            // "R Kumar", "ravi@okhdfcbank"
    {
        public Guid PayerId { get; set; }
        public required string AliasText { get; set; }
        public required string Source { get; set; }             // "Zelle", "UPI", "PayPal"
    }
}
