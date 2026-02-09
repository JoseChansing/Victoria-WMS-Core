using System;

namespace Victoria.Inventory.Domain.Aggregates
{
    public class SyncState
    {
        public string Id { get; set; } = string.Empty; // e.g., "Product", "InboundOrder"
        public DateTime LastSyncTimestamp { get; set; } = DateTime.MinValue;
        public string EntityType { get; set; } = string.Empty;
    }
}
