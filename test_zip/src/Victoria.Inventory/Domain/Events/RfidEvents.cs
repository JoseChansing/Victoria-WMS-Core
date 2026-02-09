using System;
using System.Collections.Generic;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Events
{
    public record RfidReadProcessed(
        string TenantId,
        string Epc,
        string Sku,
        string Serial,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;

    public record RfidMismatchDetected(
        string TenantId,
        string LocationCode,
        List<string> MissingEpcs,
        List<string> ExtraEpcs,
        DateTime OccurredOn,
        string CreatedBy,
        string StationId
    ) : IDomainEvent;
}
