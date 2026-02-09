using System;

namespace Victoria.Core
{
    public interface IDomainEvent
    {
        string TenantId { get; }
        DateTime OccurredOn { get; }
        string CreatedBy { get; }
        string StationId { get; }
    }
}
