using Marten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Victoria.Core.Interfaces;
using Victoria.Core.Models;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class InboundOrderSyncService : IInboundService
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<InboundOrderSyncService> _logger;

        public InboundOrderSyncService(IDocumentSession session, ILogger<InboundOrderSyncService> logger)
        {
            _session = session;
            _logger = logger;
        }

        public async Task<int> SyncAllAsync(IOdooRpcClient odooClient)
        {
            _logger.LogInformation("[DELTA-SYNC] Syncing Inbound Orders from Odoo...");

            // 1. Get Sync State
            var syncState = await _session.LoadAsync<SyncState>("InboundOrderSync") ?? new SyncState { Id = "InboundOrderSync", EntityType = "InboundOrder" };

            var domainList = new List<object[]> { 
                new object[] { "state", "in", new string[] { "assigned", "partially_available" } }, // USER REQUEST: READY/PARTIAL ONLY
                new object[] { "picking_type_code", "in", new string[] { "incoming" } }
            };

            _logger.LogInformation($"[DELTA-SYNC] Consultando órdenes en estado Assigned/Partially Available...");

            if (syncState.LastSyncTimestamp != DateTime.MinValue)
            {
                var safeFilterDate = syncState.LastSyncTimestamp.AddMinutes(-15);
                domainList.Add(new object[] { "write_date", ">", safeFilterDate.ToString("yyyy-MM-dd HH:mm:ss") });
                _logger.LogInformation("[DELTA-SYNC] Filtering orders modified after {LastSync}", syncState.LastSyncTimestamp);
                Console.WriteLine($"🕒 [SYNC-ORDER] Buscando cambios desde {safeFilterDate} (Buffer -15m aplicado)");
            }

            var domain = domainList.ToArray();

            var fields = new string[] { "name", "picking_type_code", "id", "write_date" };
            var pickings = await odooClient.SearchAndReadAsync<OdooOrderDto>("stock.picking", domain, fields);
            
            if (pickings == null || pickings.Count == 0)
            {
                _logger.LogInformation("[DELTA-SYNC] No new or modified orders found.");
                return 0;
            }

            _logger.LogInformation($"[DELTA-SYNC] Odoo returned {pickings.Count} modified inbound pickings");
            
            int processed = 0;
            foreach (var pick in pickings)
            {
                var moveDomain = new object[][] { new object[] { "picking_id", "=", pick.Id } };
                var moveFields = new string[] { "id", "product_id", "product_uom_qty" };
                
                try {
                    var moves = await odooClient.SearchAndReadAsync<OdooOrderLineDto>("stock.move", moveDomain, moveFields);
                    pick.Lines = moves;
                    await SyncPicking(pick, "INCOMING");
                    processed++;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error fetching lines for picking {Ref}", pick.Name);
                }
            }

            // 4. Update Sync State
            syncState.LastSyncTimestamp = DateTime.UtcNow;
            _session.Store(syncState);
            await _session.SaveChangesAsync();

            return processed;
        }

        private long ExtractProductId(object? productIdData)
        {
            if (productIdData == null) return 0;
            if (productIdData is System.Text.Json.JsonElement elem)
            {
                if (elem.ValueKind == System.Text.Json.JsonValueKind.Array && elem.GetArrayLength() > 0)
                    return elem[0].TryGetInt64(out var val) ? val : 0;
                if (elem.ValueKind == System.Text.Json.JsonValueKind.Number)
                    return elem.GetInt64();
            }
            if (productIdData is System.Collections.IEnumerable enumerable && !(productIdData is string))
            {
                foreach (var item in enumerable)
                {
                    if (long.TryParse(item?.ToString(), out var lVal)) return lVal;
                    break;
                }
            }
            if (long.TryParse(productIdData.ToString(), out var directVal)) return directVal;
            return 0;
        }

        public async Task SyncPicking(OdooOrderDto odooPicking, string type)
        {
            _logger?.LogInformation("[OdooSync-Marten] Persisting {Type} Picking: {Ref}", type, odooPicking.Name);

            // 1. Check for existing order to preserve local progress (ReceivedQty)
            var existingOrder = await _session.LoadAsync<InboundOrder>(odooPicking.Id.ToString());

            // STATUS GUARD: If there is local progress, ABORT Odoo Sync to prevent data loss.
            // When work starts, Victoria WMS takes full control of the order lines.
            if (existingOrder != null && existingOrder.Lines.Any(l => l.ReceivedQty > 0))
            {
                _logger.LogWarning("[SYNC-GUARD] Order {Ref} (ID: {Id}) has local progress. Skipping Odoo update to prevent data loss.", odooPicking.Name, odooPicking.Id);
                return;
            }

            var lines = new List<InboundLine>();
            foreach (var l in (odooPicking.Lines ?? new()))
            {
                long productId = ExtractProductId(l.Product_Id);

                // BUSCAR PRODUCTO PARA OBTENER SKU
                var product = await _session.Query<Product>()
                    .Where(x => x.OdooId == productId)
                    .FirstOrDefaultAsync();
                
                // 2. Find local progress: Try MoveId first, then SKU as fallback
                var existingLine = existingOrder?.Lines.FirstOrDefault(x => x.OdooMoveId == l.Id)
                                ?? existingOrder?.Lines.FirstOrDefault(x => x.Sku == product?.Sku);
                
                // URGENT FIX: Ensure ReceivedQty is strictly carried over
                int preservedQty = 0;
                if (existingLine != null) {
                    preservedQty = existingLine.ReceivedQty;
                    if (preservedQty > 0) {
                         _logger.LogInformation("[SYNC-HARDENING] Preserving local qty {Qty} for SKU {Sku}", preservedQty, product?.Sku ?? "Unknown");
                    }
                }

                var line = new InboundLine
                {
                    ExpectedQty = (int)l.Product_Uom_Qty,
                    ReceivedQty = preservedQty, // STRICT PRESERVATION
                    OdooMoveId = l.Id,
                    Sku = product?.Sku ?? $"ODOO-{l.Product_Id}"
                };

                if (product != null)
                {
                    line.ProductName = product.Name;
                    line.Brand = product.Brand ?? "";
                    line.Sides = product.Sides ?? "";
                    line.ImageSource = product.ImageSource;
                    line.Dimensions = product.PhysicalAttributes;
                }
                else
                {
                    _logger.LogWarning($"[OdooSync] Product with OdooId {l.Product_Id} not found in Marten. Using fallback SKU.");
                }
                lines.Add(line);
            }

            var order = new InboundOrder
            {
                Id = odooPicking.Id.ToString(),
                OrderNumber = odooPicking.Name,
                Supplier = "Odoo Supplier",
                Status = existingOrder?.Status ?? "Pending", // PRESERVE STATUS (e.g. Received)
                Lines = lines,
                TotalUnits = lines.Sum(l => l.ExpectedQty),
                Date = DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            _session.Store(order);
            await _session.SaveChangesAsync();
        }
    }
}
