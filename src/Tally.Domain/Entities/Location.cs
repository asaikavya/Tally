using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
    public class Location : Entity                              // optional: branch or "Online"
    {
        public Guid OrganizerId { get; set; }
        public required string Name { get; set; }
        public bool IsOnline { get; set; }
    }
}
