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

            var fields = new string[] { "name", "picking_type_code", "id", "write_date", "partner_id" };
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

        public async Task<bool> SyncSingleOrderAsync(IOdooRpcClient odooClient, string orderNumber)
        {
            _logger.LogInformation("[SINGLE-SYNC] Searching for specific order {Ref} in Odoo...", orderNumber);

            var domain = new object[][] { 
                new object[] { "name", "=", orderNumber }
            };
            var fields = new string[] { "name", "picking_type_code", "id", "write_date", "partner_id" };

            var pickings = await odooClient.SearchAndReadAsync<OdooOrderDto>("stock.picking", domain, fields);

            if (pickings == null || pickings.Count == 0)
            {
                _logger.LogWarning("[SINGLE-SYNC] Order {Ref} NOT FOUND in Odoo.", orderNumber);
                return false;
            }

            var pick = pickings[0];
            _logger.LogInformation("[SINGLE-SYNC] Found order {Ref} (ID: {Id}). Fetching lines...", orderNumber, pick.Id);

            var moveDomain = new object[][] { new object[] { "picking_id", "=", pick.Id } };
            var moveFields = new string[] { "id", "product_id", "product_uom_qty" };

            try 
            {
                var moves = await odooClient.SearchAndReadAsync<OdooOrderLineDto>("stock.move", moveDomain, moveFields);
                pick.Lines = moves;
                await SyncPicking(pick, "INCOMING");
                await _session.SaveChangesAsync();
                _logger.LogInformation("[SINGLE-SYNC] Order {Ref} injected successfully.", orderNumber);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SINGLE-SYNC] Error injecting order {Ref}", orderNumber);
                return false;
            }
        }

        private string ExtractPartnerName(object? partnerData)
        {
            if (partnerData == null) return "Odoo Hub";
            
            try 
            {
                if (partnerData is System.Text.Json.JsonElement elem)
                {
                    // Odoo returns [id, "Name"]
                    if (elem.ValueKind == System.Text.Json.JsonValueKind.Array && elem.GetArrayLength() > 1)
                        return elem[1].GetString() ?? "Unknown";
                    
                    if (elem.ValueKind == System.Text.Json.JsonValueKind.String)
                        return elem.GetString() ?? "Unknown";
                }
                
                if (partnerData is System.Collections.IList list && list.Count > 1)
                {
                    return list[1]?.ToString() ?? "Unknown";
                }
                
                // Fallback for direct string or other types
                return partnerData.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown Supplier";
            }
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

            // 1. Load Existing Order
            var existingOrder = await _session.LoadAsync<InboundOrder>(odooPicking.Id.ToString());

            // 2. SMART GUARD: Allow Active/Pending, Block Completed/Cancelled
            if (existingOrder != null)
            {
                if (existingOrder.Status == "Completed" || existingOrder.Status == "Cancelled")
                {
                    _logger.LogInformation("[SYNC-GUARD] Order {Ref} is {Status}. Skipping update.", odooPicking.Name, existingOrder.Status);
                    return;
                }
            }

            var lines = new List<InboundLine>();
            foreach (var l in (odooPicking.Lines ?? new()))
            {
                long productId = ExtractProductId(l.Product_Id);

                // Find Product Map
                var product = await _session.Query<Product>()
                    .Where(x => x.OdooId == productId)
                    .FirstOrDefaultAsync();
                
                // 3. SMART MERGE STRATEGY
                // Find matching local line to preserve progress
                var existingLine = existingOrder?.Lines.FirstOrDefault(x => x.OdooMoveId == l.Id)
                                ?? existingOrder?.Lines.FirstOrDefault(x => x.Sku == product?.Sku);
                
                int preservedReceived = 0;
                int finalExpected = (int)l.Product_Uom_Qty;
                
                if (existingLine != null) 
                {
                    // A. PRESERVE RECEIVED QTY (Critical Rule)
                    preservedReceived = existingLine.ReceivedQty;

                    // B. PRESERVE EXPECTED QTY (Critical Rule from User)
                    // We trust local truth for ExpectedQty if we are already working on it.
                    if (existingOrder?.Status != "Pending") 
                    {
                        finalExpected = existingLine.ExpectedQty;
                    }

                    // C. METADATA PATCHING (The Goal)
                    // If we found the line by SKU but it missed the MoveID, we are fixing it now effectively by assigning it below.
                    if ((existingLine.OdooMoveId == null || existingLine.OdooMoveId == 0) && l.Id > 0)
                    {
                         _logger.LogWarning($"[SYNC-MERGE] Updating Metadata for Active Order {odooPicking.Name}. MoveId {l.Id} patched for SKU {existingLine.Sku}. Quantity preserved: {preservedReceived}.");
                    }
                }

                var line = new InboundLine
                {
                    ExpectedQty = finalExpected,
                    ReceivedQty = preservedReceived, 
                    OdooMoveId = l.Id, // Always take Odoo's ID (this performs the fix)
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
                Supplier = ExtractPartnerName(odooPicking.Partner_Id),
                // Preserve status if exists, otherwise Pending
                Status = existingOrder?.Status ?? "Pending", 
                // Preserve existing date or use current server time for new orders
                Date = existingOrder?.Date ?? DateTime.Now.ToString("O"),
                Lines = lines,
                TotalUnits = lines.Sum(l => l.ExpectedQty),
                IsCrossdock = existingOrder?.IsCrossdock ?? false,
                TargetOutboundOrder = existingOrder?.TargetOutboundOrder,
                ProcessedDate = existingOrder?.ProcessedDate,
                DateClosed = existingOrder?.DateClosed
            };
            
            _session.Store(order);
            await _session.SaveChangesAsync();
        }

        public async Task<int> PerformCleanupGuardian(IOdooRpcClient odooClient)
        {
            _logger.LogInformation("[GUARDIAN] Iniciando validación de consistencia con Odoo...");

            // 1. Obtener todas las órdenes locales activas (no Completadas/Canceladas)
            var localInboundOrders = await _session.Query<InboundOrder>()
                .Where(x => x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Orphaned")
                .ToListAsync();

            if (!localInboundOrders.Any()) return 0;

            int actionCount = 0;

            foreach (var localOrder in localInboundOrders)
            {
                // 2. Verificar existencia en Odoo (Search ID)
                // Usamos un domain simple por ID
                var odooExists = await odooClient.SearchAndReadAsync<OdooOrderDto>(
                    "stock.picking", 
                    new object[][] { new object[] { "id", "=", int.Parse(localOrder.Id) } }, 
                    new string[] { "id", "state" }
                );

                if (odooExists == null || odooExists.Count == 0)
                {
                    // CASE 1: Order Deleted in Odoo
                    _logger.LogWarning($"[GUARDIAN] Order {localOrder.OrderNumber} NOT FOUND in Odoo. Processing as deleted.");
                    actionCount += await HandleMissingOrCancelledOrder(localOrder, "deleted");
                }
                else
                {
                    // CASE 2: Order Exists but Cancelled
                    var odooOrder = odooExists[0];
                    if (odooOrder.State == "cancel")
                    {
                        _logger.LogInformation($"[GUARDIAN] Order {localOrder.OrderNumber} found in Odoo with state 'cancel'. Processing as cancelled.");
                        
                        // Set DateClosed when cancelling via Guardian
                        localOrder.DateClosed = DateTime.Now;
                        _session.Store(localOrder); // Ensure this update is tracked before handling status
                        
                        actionCount += await HandleMissingOrCancelledOrder(localOrder, "cancelled");
                    }
                    else
                    {
                        // Log healthy state for debugging
                        _logger.LogDebug($"[GUARDIAN] Order {localOrder.OrderNumber} is healthy in Odoo (State: {odooOrder.State}).");
                    }
                }
            }

            await _session.SaveChangesAsync();
            return actionCount;
        }

        private async Task<int> HandleMissingOrCancelledOrder(InboundOrder localOrder, string reason)
        {
            int actionsPerformed = 0;
            // 3. Evaluar Seguridad (Guardián)
            bool hasWorkDone = localOrder.Lines.Any(l => l.ReceivedQty > 0);

            if (!hasWorkDone)
            {
                if (reason == "cancelled")
                {
                    _logger.LogInformation($"[GUARDIAN] Order {localOrder.OrderNumber} is cancelled in Odoo. Updating local status to Cancelled.");
                    localOrder.Status = "Cancelled";
                    _session.Store(localOrder);
                }
                else
                {
                    _logger.LogWarning($"[GUARDIAN] Order {localOrder.OrderNumber} matches '{reason}' criteria and has NO work. Auto-Deleting local copy.");
                    _session.Delete(localOrder);
                }
                actionsPerformed++;
            }
            else
            {
                // Verify if already orphaned to avoid spamming notifications
                if (localOrder.Status != "Orphaned")
                {
                    _logger.LogError($"[GUARDIAN] ALERTA: Orden {localOrder.OrderNumber} matches '{reason}' criteria but HAS RECEIVED INVENTORY. Marking as ORPHANED/BLOCKED.");
                    localOrder.Status = "Orphaned";
                    _session.Store(localOrder);

                    // 4. Crear Notificación de Sistema
                    var notification = new SystemNotification
                    {
                        Title = "Orden Huérfana Detectada",
                        Message = $"La orden {localOrder.OrderNumber} fue {reason} en Odoo pero tiene inventario recibido en Victoria. Se ha bloqueado para proteger la integridad.",
                        Severity = "Critical",
                        ReferenceId = localOrder.OrderNumber,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };
                    _session.Store(notification);
                    actionsPerformed++;
                }
            }
            return actionsPerformed;
        }
    }
}
