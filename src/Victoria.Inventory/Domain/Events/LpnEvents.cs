using System;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Events
{
    public record LpnCreated(
        string LpnId,
        string LpnCode,
        string Sku,
        int Quantity,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record ReceiptCompleted(
        string LpnId,
        string OrderId,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LocationChanged(
        string LpnId,
        string OldLocation,
        string NewLocation,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record PutawayCompleted(
        string LpnId,
        string LocationCode,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LpnAllocated(
        string LpnId,
        string OrderId,
        string Sku,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LpnPicked(
        string LpnId,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record PackingCompleted(
        string MasterLpnId,
        IEnumerable<string> ChildLpnIds,
        double TotalWeight,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record DispatchConfirmed(
        string OrderId,
        string DockDoor,
        IEnumerable<string> DispatchedLpnIds,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;
}
