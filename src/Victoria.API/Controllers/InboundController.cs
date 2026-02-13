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
using Victoria.Core.Models;
using Victoria.Infrastructure.Integration.Odoo;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.IO;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/inbound")]
    public class InboundController : ControllerBase
    {
        private readonly IDocumentSession _session;
        private readonly ReceiveLpnHandler _handler;
        private readonly VoidLpnHandler _voidHandler;
        private readonly IProductService _productSync;
        private readonly IInboundService _orderSync;
        private readonly IOdooRpcClient _odooClient;
        private readonly IOdooAdapter _odooAdapter;
        private readonly ILogger<InboundController> _logger;

        public InboundController(
            IDocumentSession session, 
            ReceiveLpnHandler handler,
            VoidLpnHandler voidHandler,
            IProductService productSync,
            IInboundService orderSync,
            IOdooRpcClient odooClient,
            IOdooAdapter odooAdapter,
            ILogger<InboundController> logger)
        {
            _session = session;
            _handler = handler;
            _voidHandler = voidHandler;
            _productSync = productSync;
            _orderSync = orderSync;
            _odooClient = odooClient;
            _odooAdapter = odooAdapter;
            _logger = logger;
        }

        [HttpGet("kpis")]
        public async Task<IActionResult> GetKPIs()
        {
            try
            {
                var orders = await _session.Query<InboundOrder>()
                    .Where(x => x.Status == "Pending")
                    .ToListAsync();

                var todayString = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var processedToday = await _session.Query<InboundOrder>()
                    .Where(x => x.Status == "Completed" && x.ProcessedDate == todayString)
                    .CountAsync();

                return Ok(new
                {
                    PendingOrders = orders.Count,
                    UnitsToReceive = orders.Sum(o => o.TotalUnits),
                    ProcessedToday = processedToday,
                    HighPriorityCount = 0 // Mock por ahora
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] GetKPIs failed.");
                return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
            }
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] string? mode)
        {
            try
            {
                IQueryable<InboundOrder> query = _session.Query<InboundOrder>();
                
                if (mode == "crossdock")
                {
                    query = query.Where(x => x.IsCrossdock);
                }
                else if (mode == "standard")
                {
                    query = query.Where(x => !x.IsCrossdock);
                }

                var orders = await query
                    .OrderByDescending(x => x.Date)
                    .ToListAsync();

                // Enrich with product dimensions AND current names for UI
                var skus = orders.SelectMany(o => o.Lines).Select(l => l.Sku).Distinct().ToList();
                var products = await _session.Query<Product>().Where(p => p.Sku.In(skus)).ToListAsync();
                // DEFENSIVE: Handle potential duplicate SKUs in master data
                var productDict = products.GroupBy(p => p.Sku).ToDictionary(g => g.Key, g => g.First());

                // Fetch samples already in PHOTO-STATION for these orders
                var orderIds = orders.Select(o => o.Id).ToList();
                var samplesInPhoto = await _session.Query<Lpn>()
                    .Where(x => x.SelectedOrderId.In(orderIds) && x.CurrentLocationId == "PHOTO-STATION")
                    .ToListAsync();

                var result = orders.Select(o => new {
                    o.Id,
                    o.OrderNumber,
                    o.Supplier,
                    o.Status,
                    o.Date,
                    o.TotalUnits,
                    o.IsCrossdock,
                    o.TargetOutboundOrder,
                    Lines = o.Lines.Select(l => {
                        var isSampleInPhoto = samplesInPhoto.Any(s => s.Sku.Value == l.Sku && s.SelectedOrderId == o.Id);
                        return new {
                            l.Sku,
                            // e CRITICAL FIX: Use current product name from master, fallback to stored name
                            ProductName = productDict.TryGetValue(l.Sku, out var p) ? p.Name : l.ProductName,
                            Brand = productDict.TryGetValue(l.Sku, out var pb) ? pb.Brand : (l.Brand ?? ""),
                            Sides = productDict.TryGetValue(l.Sku, out var ps) ? ps.Sides : (l.Sides ?? ""),
                            l.ExpectedQty,
                            l.ReceivedQty,
                            RequiresSample = productDict.TryGetValue(l.Sku, out var pr) ? !pr.HasImage : true,
                            SampleReceived = isSampleInPhoto,
                            Dimensions = productDict.TryGetValue(l.Sku, out var prod) ? new {
                                Weight = double.IsFinite(prod.PhysicalAttributes?.Weight ?? 0) ? prod.PhysicalAttributes?.Weight ?? 0 : 0,
                                Length = double.IsFinite(prod.PhysicalAttributes?.Length ?? 0) ? prod.PhysicalAttributes?.Length ?? 0 : 0,
                                Width = double.IsFinite(prod.PhysicalAttributes?.Width ?? 0) ? prod.PhysicalAttributes?.Width ?? 0 : 0,
                                Height = double.IsFinite(prod.PhysicalAttributes?.Height ?? 0) ? prod.PhysicalAttributes?.Height ?? 0 : 0
                            } : null,
                            Category = productDict.TryGetValue(l.Sku, out var pc) ? pc.Category : "",
                            Packagings = productDict.TryGetValue(l.Sku, out var pp) ? pp.Packagings : null
                        };
                    })
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] GetOrders failed.");
                return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
            }
        }

        [HttpPatch("orders/{id}")]
        public async Task<IActionResult> PatchOrder(string id, [FromBody] PatchInboundOrderRequest request)
        {
            try
            {
                var order = await _session.LoadAsync<InboundOrder>(id);
                if (order == null) return NotFound("Order not found");

                if (request.IsCrossdock.HasValue) order.IsCrossdock = request.IsCrossdock.Value;
                if (request.TargetOutboundOrder != null) order.TargetOutboundOrder = request.TargetOutboundOrder;
                
                // BACKDOOR: Allow status override for recovery
                if (!string.IsNullOrEmpty(request.Status)) 
                {
                     order.Status = request.Status;
                     _logger.LogWarning($"[MANUAL-Override] Order {id} status changed to {request.Status}");
                }

                _session.Store(order);
                await _session.SaveChangesAsync();

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] PatchOrder failed for order {OrderId}", id);
                return BadRequest(new { error = ex.Message });
            }
        }

        public class PatchInboundOrderRequest
        {
            public bool? IsCrossdock { get; set; }
            public string? TargetOutboundOrder { get; set; }
            public string? Status { get; set; } // Added for Manual Recovery
        }

        [HttpPost("receive")]
        public async Task<IActionResult> Receive([FromBody] ReceiveRequest request)
        {
            try
            {
                var order = await _session.LoadAsync<InboundOrder>(request.OrderId);
                if (order == null) return NotFound("Order not found");

                // SECURITY: Prevent modifications if order is closed, cancelled or orphaned
                if (order.Status == "Completed" || order.Status == "Cancelled" || order.Status == "Orphaned")
                {
                    if (order.Status == "Orphaned") {
                        return Conflict(new { error = "ORDER_ORPHANED", message = "La orden fue cancelada externamente en Odoo y tiene bultos recibidos. Operación bloqueada para nueva mercancia." });
                    }
                    return BadRequest(new { error = $"Order is {order.Status} and cannot be modified." });
                }

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
                    IsPhotoSample = request.IsPhotoSample,
                    UserId = "System", // TODO: Get from Auth
                    StationId = request.IsPhotoSample ? "PHOTO-STATION" : "STATION-01", 
                    ManualDimensions = (request.Weight.HasValue || request.Length.HasValue || request.Width.HasValue || request.Height.HasValue)
                        ? PhysicalAttributes.Create(request.Weight ?? 0, request.Length ?? 0, request.Width ?? 0, request.Height ?? 0)
                        : null
                };

                // Execute logic via Handler
                _logger.LogInformation($"[INBOUND-API] Received request for {request.Sku ?? request.RawScan}. IsPhotoSample: {request.IsPhotoSample}");
                
                var generatedIds = await _handler.Handle(command);

                // --- PACKAGING SYNC LOGIC ---
                if (!string.IsNullOrEmpty(request.PackagingAction) && (request.Weight.HasValue || request.Length.HasValue || request.Width.HasValue || request.Height.HasValue))
                {
                    try 
                    {
                        var product = await _session.LoadAsync<Product>(request.Sku ?? request.RawScan);
                        if (product != null)
                        {
                            if (request.PackagingAction == "update_odoo" && request.SelectedPackagingId.HasValue)
                            {
                                _logger.LogInformation("[INBOUND] Actualizando empaque {PkgId} en Odoo...", request.SelectedPackagingId.Value);
                                await _odooAdapter.UpdatePackagingAsync(request.SelectedPackagingId.Value, 
                                    request.UnitsPerLpn.ToString(), 
                                    request.UnitsPerLpn,
                                    request.Weight ?? 0, request.Length ?? 0, request.Width ?? 0, request.Height ?? 0);
                            }
                            else if (request.PackagingAction == "create_new")
                            {
                                if (product.OdooTemplateId == 0)
                                {
                                    _logger.LogInformation("[INBOUND] Product {Sku} missing Template ID. Syncing...", request.Sku);
                                    await _productSync.SyncSingleAsync(_odooClient, request.Sku ?? request.RawScan);
                                    product = await _session.LoadAsync<Product>(request.Sku ?? request.RawScan);
                                }

                                if (product != null && product.OdooTemplateId != 0)
                                {
                                    _logger.LogInformation("[INBOUND] Creando nuevo empaque para {Sku} en Odoo...", request.Sku);
                                    await _odooAdapter.CreatePackagingAsync(product.OdooId, product.OdooTemplateId,
                                        request.UnitsPerLpn.ToString(), 
                                        request.UnitsPerLpn,
                                        request.Weight ?? 0, request.Length ?? 0, request.Width ?? 0, request.Height ?? 0);
                                }
                                else
                                {
                                    _logger.LogWarning("[INBOUND] Falló obtención de Template ID para {Sku}. No se pudo crear empaque en Odoo.", request.Sku);
                                }
                            }

                            // Trigger refresco local
                            await _productSync.SyncSingleAsync(_odooClient, product.Sku);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[INBOUND] Error sincronizando empaque con Odoo durante recepción.");
                        // No bloqueamos el recibo por esto, pero lo logueamos
                    }
                }

                // 3. Update Order Line Case-Insensitively
                var skuToUpdate = (request.Sku ?? request.RawScan)?.Trim().ToUpperInvariant();
                var line = order.Lines.FirstOrDefault(l => l.Sku.Trim().ToUpperInvariant() == skuToUpdate);
                if (line != null)
                {
                    line.ReceivedQty += request.Quantity;

                    // Automatically transition to "In Progress" on first receipt
                    if (order.Status == "Pending")
                    {
                        _logger.LogInformation("[INBOUND] Transitioning order {OrderId} to In Progress (Receiving)", request.OrderId);
                        order.Status = "In Progress";
                    }

                    _session.Store(order);
                    await _session.SaveChangesAsync();
                }

                return Ok(new { LpnIds = generatedIds, Count = generatedIds.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] Receive failed for order {OrderId}", request.OrderId);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("lpn/{lpnId}/void")]
        public async Task<IActionResult> VoidLpn(string lpnId, [FromBody] VoidLpnRequest request)
        {
            try
            {
                var lpn = await _session.LoadAsync<Lpn>(lpnId);
                if (lpn == null) return NotFound("LPN not found");

                if (!string.IsNullOrEmpty(lpn.SelectedOrderId))
                {
                    var order = await _session.LoadAsync<InboundOrder>(lpn.SelectedOrderId);
                    if (order != null && (order.Status == "Completed" || order.Status == "Cancelled"))
                    {
                        return BadRequest(new { error = $"Order {order.OrderNumber} is {order.Status}. LPN cannot be voided." });
                    }
                }

                var command = new VoidLpnCommand(lpnId, request.Reason, "System", request.StationId);
                await _voidHandler.Handle(command);

                // --- AUTO-CANCELLATION LOGIC FOR ORPHANED ORDERS ---
                if (!string.IsNullOrEmpty(lpn.SelectedOrderId))
                {
                    var order = await _session.LoadAsync<InboundOrder>(lpn.SelectedOrderId);
                    if (order != null && order.Status == "Orphaned")
                    {
                        var remainingUnits = order.Lines.Sum(x => x.ReceivedQty);
                        if (remainingUnits <= 0)
                        {
                            _logger.LogInformation("[INBOUND] Order {OrderNumber} was Orphaned and is now EMPTY. Auto-cancelling as requested.", order.OrderNumber);
                            order.Status = "Cancelled";
                            _session.Store(order);
                            await _session.SaveChangesAsync();
                        }
                    }
                }

                return Ok(new { message = $"LPN {lpnId} voided successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] Void failed for LPN {LpnId}", lpnId);
                return BadRequest(new { error = ex.Message });
            }
        }

        public class VoidLpnRequest
        {
            public string Reason { get; set; } = string.Empty;
            public string StationId { get; set; } = string.Empty;
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

        [HttpDelete("{orderId}")]
        public async Task<IActionResult> DeleteOrder(string orderId)
        {
            var order = await _session.LoadAsync<InboundOrder>(orderId);
            if (order == null) return NotFound("Order not found");

            _logger.LogInformation($"Deleting order {orderId} and its LPNs.");

            var lpns = await _session.Query<Lpn>()
                .Where(x => x.SelectedOrderId == orderId)
                .ToListAsync();
            
            foreach (var lpn in lpns)
            {
                _session.Delete(lpn);
            }

            _session.Delete(order);
            await _session.SaveChangesAsync();

            return Ok(new { Message = "Order and its LPNs deleted successfully", OrderId = orderId });
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
                    Console.WriteLine($"[DEBUG-CLOSE] No moves found. Lines: {order.Lines.Count}");
                }
                else
                {
                    // 3. Trigger Odoo Sync (Validate + Backorder)
                    _logger.LogInformation("[INBOUND] Synchronizing with Odoo Picking {PickingId}...", pickingId);
                    Console.WriteLine($"[DEBUG-CLOSE] Calling ConfirmReceiptAsync for Picking {pickingId} with {moveQuantities.Count} moves...");
                    
                    bool odooSuccess = await _odooAdapter.ConfirmReceiptAsync(pickingId, moveQuantities);
                    
                    Console.WriteLine($"[DEBUG-CLOSE] ConfirmReceiptAsync returned: {odooSuccess}");

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

                    // OVERRIDE: Everything from DOCK-LPN goes to STAGE-RESERVE (User request)
                    if (lpn.CurrentLocationId == "DOCK-LPN")
                    {
                        targetLocation = "STAGE-RESERVE";
                    }

                    lpn.Putaway(targetLocation, "SYS", "ODOO-SYNC-CLOSE");
                    _session.Store(lpn);
                }
            }

            // 4. Mark order as closed/synced
            order.Status = "Completed";
            order.ProcessedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
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

        [HttpGet("debug/inspect-reception")]
        public async Task<IActionResult> DebugInspectReception([FromQuery] string orderNumber)
        {
            try
            {
                var order = await _session.Query<InboundOrder>()
                    .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

                if (order == null) return NotFound($"Order {orderNumber} not found");

                var lpns = await _session.Query<Lpn>()
                    .Where(l => l.SelectedOrderId == order.Id)
                    .Select(l => new { l.Id, l.Sku, l.CurrentLocationId, l.Status })
                    .ToListAsync();

                return Ok(new { 
                    Order = order.OrderNumber, 
                    Lpns = lpns 
                });
            }
            catch (Exception ex)
            {
                 return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("debug/fix-reception")]
        public async Task<IActionResult> DebugFixReception([FromQuery] string orderNumber)
        {
            try
            {
                var order = await _session.Query<InboundOrder>()
                    .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

                if (order == null) return NotFound($"Order {orderNumber} not found");

                // Find LPNs for this order
                var lpns = await _session.Query<Lpn>()
                    .Where(l => l.SelectedOrderId == order.Id)
                    .ToListAsync();

                if (!lpns.Any()) return Ok("No LPNs found for this order.");

                _logger.LogWarning($"[FIX] Deleting {lpns.Count} LPNs for order {orderNumber}");

                foreach (var lpn in lpns)
                {
                    // Correct Order Line
                    var line = order.Lines.FirstOrDefault(x => x.Sku == lpn.Sku.Value);
                    if (line != null)
                    {
                        line.ReceivedQty = Math.Max(0, line.ReceivedQty - lpn.Quantity);
                    }

                    // Delete LPN
                    _session.Delete(lpn);
                }

                _session.Store(order);
                await _session.SaveChangesAsync();

                return Ok(new { 
                    Message = $"Deleted {lpns.Count} LPNs and updated order quantities.", 
                    DeletedLpns = lpns.Select(x => x.Id) 
                });
            }
            catch (Exception ex)
            {
                 return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("debug/odoo-status/{id}")]
        public async Task<IActionResult> DebugGetOdooStatus(long id)
        {
             try 
             {
                // Inspect raw Odoo status
                var result = await _odooClient.SearchAndReadAsync<object>("stock.picking",
                    new object[][] { new object[] { "id", "=", id } }, 
                    new string[] { "name", "state", "date_done", "picking_type_code", "move_ids" });

                // Check for created backorders
                var backorders = await _odooClient.SearchAndReadAsync<object>("stock.picking",
                    new object[][] { new object[] { "backorder_id", "=", id } },
                    new string[] { "name", "state" });

                return Ok(new { Picking = result, Backorders = backorders });
             }
             catch(Exception ex)
             {
                 return BadRequest(ex.Message);
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
        [JsonPropertyName("isPhotoSample")]
        public bool IsPhotoSample { get; set; }

        // Odoo Packaging Sync
        public string? PackagingAction { get; set; } // "update_odoo" or "create_new"
        public int? SelectedPackagingId { get; set; }
    }
}
