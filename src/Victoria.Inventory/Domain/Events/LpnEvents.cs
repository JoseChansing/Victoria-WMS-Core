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
}
