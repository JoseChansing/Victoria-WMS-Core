using System;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Events
{
    public record LocationCreated(
        string LocationCode,
        string Zone,
        Aggregates.LocationProfile Profile,
        bool IsPickable,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LocationAssigned(
        string LocationCode,
        string LpnCode,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record LocationMetadataUpdated(
        string LocationCode,
        int PickingSequence,
        double MaxWeight,
        double MaxVolume,
        string Barcode,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;
}
