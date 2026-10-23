using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
    public class WebhookEvent : Entity                          // "letters already opened"
    {
        public required string PayPalEventId { get; set; }
        public required string EventType { get; set; }
        public DateTime ReceivedAtUtc { get; set; }
        public DateTime? ProcessedAtUtc { get; set; }
    }
}
