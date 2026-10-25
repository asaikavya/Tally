using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
//using Tally.Domain.Entities;

namespace Tally.Domain.Entities
{
    public class Payer : Entity                                 // often a parent, or a client
    {
        public required string Name { get; set; }
        public string? Email { get; set; }
        public string PreferredLanguage { get; set; } = "en";
        public string PayToken { get; set; } = NewToken();

        public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }
}
