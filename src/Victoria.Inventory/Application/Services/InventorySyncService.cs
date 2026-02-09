using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Microsoft.Extensions.Logging;
using Victoria.Core.Interfaces;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.Events;

namespace Victoria.Inventory.Application.Services
{
    public class StockQuantDto
    {
        public int Id { get; set; }
        public int Product_Id { get; set; } // OdooRpcClient maps first element of M2o array to the value
        public double Quantity { get; set; }
        public int Location_Id { get; set; }
        public string In_Date { get; set; }
    }

    public class InventorySyncService
    {
        private readonly IOdooRpcClient _odooClient;
        private readonly IDocumentSession _session;
        private readonly ILogger<InventorySyncService> _logger;

        public InventorySyncService(IOdooRpcClient odooClient, IDocumentSession session, ILogger<InventorySyncService> logger)
        {
            _odooClient = odooClient;
            _session = session;
            _logger = logger;
        }

        public async Task<int> SyncInventoryAsync(string userId = "SYSTEM", string stationId = "API")
        {
            _logger.LogInformation("Starting Inventory Sync (Seed) from Odoo...");

            // 1. Fetch Quants from Odoo
            var domain = new object[][]
            {
                new object[] { "location_id.usage", "=", "internal" },
                new object[] { "quantity", ">", 0 }
            };

            var fields = new[] { "id", "product_id", "quantity", "location_id", "in_date" };
            
            // Call generic SearchAndReadAsync
            var quants = await _odooClient.SearchAndReadAsync<StockQuantDto>("stock.quant", domain, fields);

            _logger.LogInformation($"Fetched {quants.Count} quants from Odoo.");

            int importedCount = 0;

            foreach (var quant in quants)
            {
                try
                {
                    int odooProductId = quant.Product_Id;
                    
                    var productDoc = await _session.Query<Product>().FirstOrDefaultAsync(p => p.OdooId == odooProductId);
                    string sku = productDoc?.Sku ?? $"UNKNOWN-{odooProductId}";
                    string description = productDoc?.Description ?? "";
                    if (description == "0" || description == "false") description = "";

                    int quantity = (int)Math.Ceiling(quant.Quantity);
                    string targetLocation = "STAGE-PICKING";
                    string lpnId = $"LPN-INIT-{sku}";

                    var importDate = DateTime.TryParse(quant.In_Date, out var dt) ? dt : DateTime.UtcNow;

                    var evt = new InventoryImportedFromOdoo(
                        LpnId: lpnId,
                        OdooQuantId: quant.Id,
                        Sku: sku,
                        Description: description,
                        Quantity: quantity,
                        TargetLocation: targetLocation,
                        ImportDate: importDate,
                        UserId: userId,
                        StationId: stationId
                    );
                    
                    _session.Events.Append(lpnId, evt);
                    importedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to process quant {quant.Id}");
                }
            }

            await _session.SaveChangesAsync();
            _logger.LogInformation($"Inventory Sync Completed. Imported {importedCount} LPNs.");
            return importedCount;
        }
    }
}
