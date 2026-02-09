using System;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Events
{
    public record LocationCreated(
        string TenantId,
        string LocationCode,
        string Zone,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LocationAssigned(
        string TenantId,
        string LocationCode,
        string LpnCode,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;
}
