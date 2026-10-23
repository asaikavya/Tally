using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
    public enum OrganizerType { Teacher, Freelancer, Coach, EventOrganizer, Roommates, Community }
    public enum BillingFrequency { Monthly, Yearly, OneTime }
    public enum ChargeStatus { Pending, Partial, Paid, Overdue, Paused, Refunded }
    public enum PaymentSource { PayPal, Zelle, Upi, Cash, Other }
    public enum MatchedBy { Webhook, Ai, Manual }
}
