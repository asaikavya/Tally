using System;
using System.Collections.Generic;
using System.Text;

namespace Tally.Domain.Entities
{
    public abstract class Entity
    {
        public Guid Id { get; init; } = Guid.CreateVersion7();   // time-ordered, index-friendly
    }
}
