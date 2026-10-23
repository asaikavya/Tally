using System;
using System.Collections.Generic;
using System.Text;
//using Tally.Domain.Entities;

namespace Tally.Domain.Entities
{
    public class Payer : Entity                                 // often a parent, or a client
    {
        public required string Name { get; set; }
        public string? Email { get; set; }
        public string PreferredLanguage { get; set; } = "en";
    }
}
