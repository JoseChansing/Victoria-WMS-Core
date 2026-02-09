using System;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Events
{
    public record InventoryImportedFromOdoo(
        string LpnId,
        int OdooQuantId,
        string Sku,
        string Description,
        int Quantity,
        string TargetLocation,
        DateTime ImportDate,
        string UserId,
        string StationId
    ) : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredOn { get; init; } = ImportDate;
        public string CreatedBy => UserId;
    }
}
