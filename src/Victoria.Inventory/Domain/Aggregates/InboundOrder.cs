using System;
using System.Collections.Generic;

namespace Victoria.Inventory.Domain.Aggregates
{
    public class InboundOrder
    {
        public string Id { get; set; } = string.Empty; // PO Number o UUID
        public string OrderNumber { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
        public int TotalUnits { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public List<InboundLine> Lines { get; set; } = new();
    }

    public class InboundLine
    {
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int ExpectedQty { get; set; }
        public int ReceivedQty { get; set; }
        public string? ImageSource { get; set; }
    }
}
