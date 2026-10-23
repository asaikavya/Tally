using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
    public class Enrollment : Entity
    {
        public Guid MemberId { get; set; }
        public Member? Member { get; set; }
        public Guid? GroupId { get; set; }
        public Guid PricePlanId { get; set; }
        public PricePlan? PricePlan { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }
}
