using System;
using System.Collections.Generic;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Events
{
    public record LpnCreated(
        string TenantId,
        string LpnId,
        string LpnCode,
        string Sku,
        int Quantity,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LpnReceived(
        string TenantId,
        string LpnId,
        string OrderId,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LpnLocationChanged(
        string TenantId,
        string LpnId,
        string NewLocation,
        string OldLocation,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record PutawayCompleted(
        string TenantId,
        string LpnId,
        string LocationCode,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LpnAllocated(
        string TenantId,
        string LpnId,
        string OrderId,
        string Sku,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LpnPicked(
        string TenantId,
        string LpnId,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record PackingCompleted(
        string TenantId,
        string MasterLpnId,
        IEnumerable<string> ChildLpnIds,
        double TotalWeight,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record DispatchConfirmed(
        string TenantId,
        string OrderId,
        string DockDoor,
        IEnumerable<string> DispatchedLpnIds,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;
}
