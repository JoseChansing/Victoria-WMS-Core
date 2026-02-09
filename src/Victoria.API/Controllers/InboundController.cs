using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Marten;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Application.Commands;
using Victoria.Core.Interfaces;
using Victoria.Infrastructure.Integration.Odoo;
using Microsoft.Extensions.Logging;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/inbound")]
    public class InboundController : ControllerBase
    {
        private readonly IDocumentSession _session;
        private readonly ReceiveLpnHandler _handler;
        private readonly ProductSyncService _productSync;
        private readonly InboundOrderSyncService _orderSync;
        private readonly IOdooRpcClient _odooClient;
        private readonly IOdooAdapter _odooAdapter;
        private readonly ILogger<InboundController> _logger;

        public InboundController(
            IDocumentSession session, 
            ReceiveLpnHandler handler,
            ProductSyncService productSync,
            InboundOrderSyncService orderSync,
            IOdooRpcClient odooClient,
            IOdooAdapter odooAdapter,
            ILogger<InboundController> logger)
        {
            _session = session;
            _handler = handler;
            _productSync = productSync;
            _orderSync = orderSync;
            _odooClient = odooClient;
            _odooAdapter = odooAdapter;
            _logger = logger;
        }

        [HttpGet("kpis")]
        public async Task<IActionResult> GetKPIs()
        {
            var orders = await _session.Query<InboundOrder>()
                .Where(x => x.Status == "Pending")
                .ToListAsync();

            return Ok(new
            {
                PendingOrders = orders.Count,
                UnitsToReceive = orders.Sum(o => o.TotalUnits),
                HighPriorityCount = 0 // Mock por ahora
            });
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _session.Query<InboundOrder>()
                .OrderByDescending(x => x.Date)
                .ToListAsync();

            // Enrich with product dimensions AND current names for UI
            var skus = orders.SelectMany(o => o.Lines).Select(l => l.Sku).Distinct().ToList();
            var products = await _session.Query<Product>().Where(p => p.Sku.In(skus)).ToListAsync();
            var productDict = products.ToDictionary(p => p.Sku);

            var result = orders.Select(o => new {
                o.Id,
                o.OrderNumber,
                o.Supplier,
                o.Status,
                o.Date,
                o.TotalUnits,
                Lines = o.Lines.Select(l => new {
                    l.Sku,
                    // ✅ CRITICAL FIX: Use current product name from master, fallback to stored name
                    ProductName = productDict.TryGetValue(l.Sku, out var p) ? p.Name : l.ProductName,
                    l.ExpectedQty,
                    l.ReceivedQty,
                    Dimensions = productDict.TryGetValue(l.Sku, out var prod) ? new {
                        Weight = prod.PhysicalAttributes?.Weight ?? 0,
                        Length = prod.PhysicalAttributes?.Length ?? 0,
                        Width = prod.PhysicalAttributes?.Width ?? 0,
                        Height = prod.PhysicalAttributes?.Height ?? 0
                    } : null
                })
            });

            return Ok(result);
        }


        [HttpPost("receive")]
        public async Task<IActionResult> Receive([FromBody] ReceiveRequest request)
        {
            var order = await _session.LoadAsync<InboundOrder>(request.OrderId);
            if (order == null) return NotFound("Order not found");

            // Build Command for the Handler
            var command = new ReceiveLpnCommand
            {
                OrderId = request.OrderId,
                LpnId = request.LpnId,
                RawScan = request.RawScan,
                Sku = request.Sku,
                ReceivedQuantity = request.Quantity,
                ExpectedQuantity = request.ExpectedQuantity > 0 ? request.ExpectedQuantity : request.Quantity,
                LpnCount = request.LpnCount,
                UnitsPerLpn = request.UnitsPerLpn,
                IsUnitMode = request.IsUnitMode, // PATCH FINAL: Activación de modo suelto
                UserId = "System", // TODO: Get from Auth
                StationId = "STATION-01", // TODO: Get from station context
                ManualDimensions = (request.Weight.HasValue || request.Length.HasValue || request.Width.HasValue || request.Height.HasValue)
                    ? PhysicalAttributes.Create(request.Weight ?? 0, request.Length ?? 0, request.Width ?? 0, request.Height ?? 0)
                    : null
            };

            // Execute logic via Handler
            var generatedIds = await _handler.Handle(command);

            // 3. Update Order Line
            var skuToUpdate = request.Sku ?? request.RawScan;
            var line = order.Lines.FirstOrDefault(l => l.Sku == skuToUpdate);
            if (line != null)
            {
                line.ReceivedQty += request.Quantity;
                _session.Store(order);
                await _session.SaveChangesAsync();
            }

            return Ok(new { LpnIds = generatedIds, Count = generatedIds.Count });
        }

        [HttpPost("reset/{orderId}")]
        public async Task<IActionResult> ResetOrder(string orderId)
        {
            var order = await _session.LoadAsync<InboundOrder>(orderId);
            if (order == null) return NotFound("Order not found");

            _logger.LogInformation($"Resetting order {orderId}. Deleting LPNs and resetting quantities.");

            var lpns = await _session.Query<Lpn>()
                .Where(x => x.SelectedOrderId == orderId)
                .ToListAsync();
            
            foreach (var lpn in lpns)
            {
                _session.Delete(lpn);
            }

            foreach (var line in order.Lines)
            {
                line.ReceivedQty = 0;
            }

            _session.Store(order);
            await _session.SaveChangesAsync();

            return Ok(new { Message = "Order reset successfully", OrderId = orderId });
        }

        [HttpPost("{id}/close")]
        public async Task<IActionResult> Close(string id)
        {
            var order = await _session.LoadAsync<InboundOrder>(id);
            if (order == null) return NotFound("Order not found");

            _logger.LogInformation("[INBOUND] Closing receipt for order {OrderNumber} ({OrderId})", order.OrderNumber, id);

            try
            {
                // 1. HARDENING: Validate Order ID Format (Fix for "1" vs numeric)
                if (!long.TryParse(order.Id, out long pickingId))
                {
                    _logger.LogError("[INBOUND] Invalid Order ID format: {Id}. Must be a valid Odoo Picking ID (long).", order.Id);
                    return BadRequest(new { Error = $"El ID de la orden ({order.Id}) no es un ID válido para Odoo (Picking ID)." });
                }

                // 2. AGGREGATION: include ALL lines with OdooMoveId, even those with ReceivedQty == 0
                // This forces Odoo to detect partial receipts and show the Backorder Wizard.
                var moveQuantities = order.Lines
                    .Where(l => l.OdooMoveId.HasValue && l.OdooMoveId > 0)
                    .GroupBy(l => l.OdooMoveId!.Value)
                    .ToDictionary(
                        g => g.Key, 
                        g => g.Sum(x => x.ReceivedQty)
                    );

                if (moveQuantities.Count == 0)
                {
                    _logger.LogWarning("[INBOUND] No Odoo Move IDs found for order {OrderNumber}. Local closing only.", order.OrderNumber);
                }
                else
                {
                    // 3. Trigger Odoo Sync (Validate + Backorder)
                    _logger.LogInformation("[INBOUND] Synchronizing with Odoo Picking {PickingId}...", pickingId);
                    bool odooSuccess = await _odooAdapter.ConfirmReceiptAsync(pickingId, moveQuantities);
                    
                    if (!odooSuccess)
                    {
                        return BadRequest(new { Error = "La sincronización con Odoo no confirmó éxito. Verifique directamente en Odoo." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INBOUND] Fallo crítico sincronizando con Odoo para orden {OrderNumber}", order.OrderNumber);
                var detail = ex.InnerException?.Message ?? ex.Message;
                return BadRequest(new { Error = $"Odoo Error: {detail}" });
            }

            // 3. Intelligent Routing (Switch)
            var lpns = await _session.Query<Lpn>()
                .Where(x => x.SelectedOrderId == id)
                .ToListAsync();

            foreach (var lpn in lpns)
            {
                // PATCH: Consolidation of Loose Units
                if (lpn.Type == LpnType.Loose && !lpn.Id.StartsWith("LOOSE-DOCK-"))
                {
                    var masterId = $"LOOSE-DOCK-{lpn.Sku.Value}";
                    var masterLpn = await _session.LoadAsync<Lpn>(masterId);

                    if (masterLpn != null)
                    {
                        // Merge into existing Master Bucket
                        masterLpn.AddQuantity(lpn.Quantity, "System", "CONSOLIDATION-CLOSE");
                        // Ensure master is in STAGE-PICKING (in case it drifted)
                        if (masterLpn.CurrentLocationId != "STAGE-PICKING")
                        {
                            masterLpn.Putaway("STAGE-PICKING", "System", "CONSOLIDATION-MOVE");
                        }
                        _session.Store(masterLpn);
                        
                        // Delete the temporary Order LPN
                        _session.Delete(lpn);
                        _logger.LogInformation($"[INBOUND] Consolidated {lpn.Id} ({lpn.Quantity}) into existing {masterId}.");
                    }
                    else
                    {
                        // Transform Temporary LPN into Master Bucket (New Creation)
                        // Since we can't rename ID easily, we create new and delete old.
                        var newMaster = Lpn.Provision(
                            masterId, 
                            lpn.Code, // Reuse code or generate new? Use same code for tracking.
                            lpn.Sku, 
                            LpnType.Loose, 
                            lpn.Quantity, 
                            lpn.PhysicalAttributes, 
                            "System", 
                            "System");
                        
                        // Initial placement directly to Stage
                        // Note: Lpn.Create makes it "Created". Putaway invalidates "Created".
                        // Need to verify state transitions. Putaway works from Created/Received.
                        // But we need to simulate the reception event? No, Lpn.Create adds LpnCreated event.
                        
                        // Simulate Reception on new LPN? Not strictly needed for inventory, but good for history.
                        newMaster.Receive(id, "System", "System"); 
                        newMaster.Putaway("STAGE-PICKING", "System", "CONSOLIDATION-INIT");
                        
                        _session.Store(newMaster);
                        _session.Delete(lpn);
                        _logger.LogInformation($"[INBOUND] Promoted {lpn.Id} to new Master Bucket {masterId}.");
                    }
                }
                else
                {
                    // Standard Logic for Pallets or existing Masters
                    string targetLocation = lpn.Type switch
                    {
                        LpnType.Pallet => "STAGE-RESERVE",
                        LpnType.Loose => "STAGE-PICKING",
                        _ => "STAGE-PICKING" // Default
                    };

                    lpn.Putaway(targetLocation, "SYS", "ODOO-SYNC-CLOSE");
                    _session.Store(lpn);
                }
            }

            // 4. Mark order as closed/synced
            order.Status = "Completed";
            _session.Store(order);

            await _session.SaveChangesAsync();

            return Ok(new { 
                Message = "Orden sincronizada y cerrada exitosamente en Odoo.", 
                OrderId = id,
                Status = order.Status
            });
        }


        [HttpPost("sync/force")]
        public async Task<IActionResult> ForceSync()
        {
            Console.WriteLine("[API] Manual Sync Triggered...");
            try 
            {
                var productCount = await _productSync.SyncAllAsync(_odooClient);
                var orderCount = await _orderSync.SyncAllAsync(_odooClient);

                return Ok(new { 
                    Message = "Sync Completed", 
                    ProductsProcessed = productCount, 
                    OrdersProcessed = orderCount 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }

    public class ReceiveRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string? LpnId { get; set; }
        public string? RawScan { get; set; }
        public int Quantity { get; set; }
        public int ExpectedQuantity { get; set; }
        public int LpnCount { get; set; } = 1;
        public int UnitsPerLpn { get; set; }
        
        // Editable Physical Attributes
        public double? Weight { get; set; }
        public double? Length { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public bool IsUnitMode { get; set; } // PATCH FINAL: Propagación desde UI
    }
}
