using System;
using System.Collections.Generic;
using System.Text;
using Tally.Domain.Entities;

namespace Tally.Domain
{
    public class Member: Entity
    {
        public Guid PayerId { get; set; }
        public Payer? Payer { get; set; }
        public required string Name { get; set; }
        public string Country { get; set; } = "US";
        public string Currency { get; set; } = "USD";
        public bool IsActive { get; set; } = true;
    }
}
