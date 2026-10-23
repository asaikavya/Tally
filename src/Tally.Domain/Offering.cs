using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
   
    public class Offering : Entity
    {
        public Guid OrganizerId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
    }
}
