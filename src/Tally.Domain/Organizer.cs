using System;
using System.Collections.Generic;
using System.Text;
using Tally.Domain.Entities;

namespace Tally.Domain.Entities
{
    public class Organizer : Entity
    {
        public required string Name { get; set; }
        public OrganizerType Type { get; set; }
        public required string TimeZone { get; set; }          // "America/Los_Angeles"
        public string DefaultCurrency { get; set; } = "USD";
        public int DueDayOfMonth { get; set; } = 10;
    }
}
