using Marten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooOrderLineDto
    {
        public int Product_Id { get; set; }
        public double Product_Uom_Qty { get; set; }
    }

    public class OdooOrderDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // OrderNumber
        public int Company_Id { get; set; }
        public string Picking_Type_Code { get; set; } = string.Empty;
        public List<OdooOrderLineDto> Lines { get; set; } = new();
    }

    public class InboundOrderSyncService
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<InboundOrderSyncService> _logger;
        private readonly string _tenantId;

        public InboundOrderSyncService(IDocumentSession session, ILogger<InboundOrderSyncService> logger, IConfiguration config)
        {
            _session = session;
            _logger = logger;
            _tenantId = config["App:TenantId"] ?? "PERFECTPTY";
        }

        public async Task SyncPicking(OdooOrderDto odooPicking, string type)
        {
            string tenantId = _tenantId;
            _logger?.LogInformation("[OdooSync-Marten] Persisting {Type} Picking: {Ref} for {Tenant}", type, odooPicking.Name, tenantId);

            var lines = new List<InboundLine>();
            foreach (var l in (odooPicking.Lines ?? new()))
            {
                var line = new InboundLine
                {
                    ExpectedQty = (int)l.Product_Uom_Qty,
                    ReceivedQty = 0
                };

                // BUSCAR SKU Y METADATOS VIA MARTEN
                var product = await _session.Query<Product>()
                    .Where(x => x.OdooId == l.Product_Id && x.TenantId == tenantId)
                    .FirstOrDefaultAsync();
                
                if (product != null)
                {
                    line.Sku = product.Sku;
                    line.ProductName = product.Name;
                    line.ImageSource = product.ImageSource;
                }
                else
                {
                    line.Sku = $"ODOO-{l.Product_Id}";
                    _logger.LogWarning("[OdooSync] Product with OdooId {Id} not found in Marten. Using fallback SKU.", l.Product_Id);
                }
                lines.Add(line);
            }

            var order = new InboundOrder
            {
                Id = odooPicking.Id.ToString(),
                OrderNumber = odooPicking.Name,
                Supplier = "Odoo Supplier",
                Status = "Pending",
                TenantId = tenantId,
                Lines = lines,
                TotalUnits = lines.Sum(l => l.ExpectedQty)
            };

            _session.Store(order);
            await _session.SaveChangesAsync();
        }
    }
}
