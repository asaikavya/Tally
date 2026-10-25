using System;
using System.Collections.Generic;
using System.Text;
//using Tally.Domain.Entities;

namespace Tally.Domain.Entities
{
    public class PricePlan : Entity                             // "Piano monthly $100"
    {
        public Guid OfferingId { get; set; }
        public Offering? Offering { get; set; }
        public required string Name { get; set; }
        public BillingFrequency Frequency { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        
    }
}
