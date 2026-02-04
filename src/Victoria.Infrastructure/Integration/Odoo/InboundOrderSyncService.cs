using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooOrderLineDto
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class OdooOrderDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public List<OdooOrderLineDto> Lines { get; set; } = new();
    }

    public class InboundOrderSyncService
    {
        private static readonly Dictionary<int, string> TenantMapping = new()
        {
            { 1, "PERFECTPTY" }, { 2, "NATSUKI" }
        };

        public async Task SyncOrder(OdooOrderDto odooOrder)
        {
            if (!TenantMapping.TryGetValue(odooOrder.CompanyId, out var tenantId))
                throw new ArgumentException("Invalid Company");

            // ACL LOGIC: Agrupación de líneas (Deduplicación)
            var cleanLines = odooOrder.Lines
                .GroupBy(l => l.ProductId)
                .Select(g => new { Sku = g.Key, Qty = g.Sum(x => x.Quantity) });

            Console.WriteLine($"[ACL] Order {odooOrder.OrderNumber} grouped from {odooOrder.Lines.Count} to {cleanLines.Count()} unique SKU lines.");

            var order = new OutboundOrder(tenantId, odooOrder.OrderNumber);
            foreach (var line in cleanLines)
            {
                order.AddLine(Guid.NewGuid().ToString(), line.Sku, line.Qty);
            }

            // Simulación persistencia
            await Task.CompletedTask;
        }
    }
}
