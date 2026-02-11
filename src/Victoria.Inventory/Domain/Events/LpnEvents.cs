using System;
using System.Collections.Generic;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Events
{
    public record LpnCreated(
        string LpnId,
        string LpnCode,
        string Sku,
        Aggregates.LpnType Type,
        int Quantity,
        Victoria.Inventory.Domain.ValueObjects.PhysicalAttributes PhysicalAttributes,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId,
        string Brand = "",
        string Sides = "",
        string ProductBarcode = ""
    ) : IDomainEvent;

    public record LpnReceived(
        string LpnId,
        string OrderId,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LpnLocationChanged(
        string LpnId,
        string NewLocation,
        string OldLocation,
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

    public record InventoryCountCompleted(
        string LpnId,
        int ExpectedQuantity,
        int CountedQuantity,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record InventoryAdjusted(
        string LpnId,
        int OldQuantity,
        int NewQuantity,
        string ReasonCode,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LpnQuarantined(
        string LpnId,
        string Reason,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;
    public record ReceiptClosedWithShortage(
        string OrderId,
        string Sku,
        int RequestedQuantity,
        int ReceivedQuantity,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record PickShortageDetected(
        string LpnId,
        string OrderId,
        int ExpectedQuantity,
        int FoundQuantity,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;
}
