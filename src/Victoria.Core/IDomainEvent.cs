using System;

namespace Victoria.Core
{
    public interface IDomainEvent
    {
        DateTime OccurredOn { get; }
        string CreatedBy { get; }
        string StationId { get; }
    }
}
