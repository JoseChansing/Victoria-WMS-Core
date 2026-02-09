using System;
using System.Collections.Generic;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Events
{
    public record PickingOverageRequested(
        string OrderId,
        string LineId,
        string Sku,
        int OrderedQuantity,
        int RequestedQuantity,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record PickingOverageApproved(
        string OrderId,
        string LineId,
        int NewQuantity,
        string SupervisorId,
        string ReasonCode,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record PickingOverageRejected(
        string OrderId,
        string LineId,
        string SupervisorId,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;
}
