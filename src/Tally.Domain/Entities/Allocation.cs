using System;
using System.Collections.Generic;
using System.Text;
using Tally.Domain.Entities;

namespace Tally.Domain.Entities
{
    public class Allocation : Entity                            // the staple: payment ↔ charge
    {
        public Guid PaymentId { get; set; }
        public Guid ChargeId { get; set; }
        public decimal Amount { get; set; }
        public MatchedBy MatchedBy { get; set; }
        public decimal? Confidence { get; set; }
        public string? Reasons { get; set; }
    }
}
