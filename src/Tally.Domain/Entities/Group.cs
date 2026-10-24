using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
    public class Group : Entity                                 // optional: "Piano Tue 5pm"
    {
        public Guid OfferingId { get; set; }
        public Guid? LocationId { get; set; }
        public required string Name { get; set; }
        public string? Schedule { get; set; }
    }
}
