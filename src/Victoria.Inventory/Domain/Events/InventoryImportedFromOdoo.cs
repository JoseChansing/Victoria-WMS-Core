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
        string User,
        DateTime OccurredOn,
        string StationId
    ) : IDomainEvent
    {
        public string CreatedBy => User;
    }
}
