using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Marten;
using Victoria.Inventory.Application.Commands;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.Exceptions;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly ReceiveLpnHandler _receiveHandler;
        private readonly PutawayLpnHandler _putawayHandler;
        private readonly AllocateOrderHandler _allocateHandler;
        private readonly PickLpnHandler _pickHandler;
        private readonly PackingHandler _packingHandler;
        private readonly Victoria.Inventory.Application.Services.DispatchService _dispatchService;
        private readonly Victoria.Inventory.Application.Services.CycleCountService _countService;
        private readonly ApproveReceiptOverageHandler _approveReceiptOverageHandler;
        private readonly Victoria.Core.Messaging.IMessageBus _bus;
        private readonly IQuerySession _session;

        public InventoryController(
            ReceiveLpnHandler receiveHandler, 
            PutawayLpnHandler putawayHandler,
            AllocateOrderHandler allocateHandler,
            PickLpnHandler pickHandler,
            PackingHandler packingHandler,
            Victoria.Inventory.Application.Services.DispatchService dispatchService,
            Victoria.Inventory.Application.Services.CycleCountService countService,
            ApproveReceiptOverageHandler approveReceiptOverageHandler,
            Victoria.Core.Messaging.IMessageBus bus,
            IQuerySession session)
        {
            _receiveHandler = receiveHandler;
            _putawayHandler = putawayHandler;
            _allocateHandler = allocateHandler;
            _pickHandler = pickHandler;
            _packingHandler = packingHandler;
            _dispatchService = dispatchService;
            _countService = countService;
            _approveReceiptOverageHandler = approveReceiptOverageHandler;
            _bus = bus;
            _session = session;
        }

        [HttpGet]
        public async Task<IActionResult> GetInventory()
        {
            var lpns = await _session.Query<Lpn>()
                .ToListAsync();

            var items = lpns.Select(l => new
            {
                Id = l.Id,
                Sku = l.Sku.Value,
                Quantity = l.Quantity,
                Status = l.Status.ToString(),
                Location = l.CurrentLocation ?? "N/A"
            });

            return Ok(items);
        }

        [HttpPost("receipt")]
        public async Task<IActionResult> ReceiveLpn([FromBody] ReceiveLpnRequest request)
        {
            try
            {
                await _receiveHandler.Handle(new ReceiveLpnCommand
                {
                    TenantId = null,
                    LpnId = request.LpnId,
                    OrderId = request.OrderId,
                    ExpectedQuantity = request.ExpectedQuantity,
                    ReceivedQuantity = request.ReceivedQuantity,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "LPN received successfully", LpnId = request.LpnId });
            }
            catch (InvalidOperationException ex) { return Conflict(new { Error = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("putaway")]
        public async Task<IActionResult> PutawayLpn([FromBody] PutawayLpnRequest request)
        {
            try
            {
                await _putawayHandler.Handle(new PutawayLpnCommand
                {
                    TenantId = null,
                    LpnId = request.LpnId,
                    LocationCode = request.LocationCode,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "Putaway completed successfully", LpnId = request.LpnId });
            }
            catch (InvalidOperationException ex) { return Conflict(new { Error = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("allocate")]
        public async Task<IActionResult> AllocateOrder([FromBody] AllocateOrderRequest request)
        {
            try
            {
                await _allocateHandler.Handle(new AllocateOrderCommand
                {
                    TenantId = null,
                    OrderId = request.OrderId,
                    Sku = request.Sku,
                    Quantity = request.Quantity,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "Allocation successful", OrderId = request.OrderId });
            }
            catch (InvalidOperationException ex) { return Conflict(new { Error = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("pick")]
        public async Task<IActionResult> PickLpn([FromBody] PickLpnRequest request)
        {
            try
            {
                await _pickHandler.Handle(new PickLpnCommand
                {
                    TenantId = null,
                    LpnId = request.LpnId,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "LPN picked successfully", LpnId = request.LpnId });
            }
            catch (InvalidOperationException ex) { return Conflict(new { Error = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("pack")]
        public async Task<IActionResult> PackLpns([FromBody] PackLpnsRequest request)
        {
            try
            {
                await _packingHandler.Handle(new PackLpnsCommand
                {
                    TenantId = null,
                    MasterLpnId = request.MasterLpnId,
                    ChildLpnIds = request.ChildLpnIds,
                    Weight = request.Weight,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "Packing completed. Master container created.", MasterLpnId = request.MasterLpnId });
            }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("dispatch/{id}")]
        public async Task<IActionResult> DispatchOrder(string id, [FromBody] DispatchOrderRequest request)
        {
            try
            {
                var zpl = await _dispatchService.DispatchOrder(null, id, request.DockDoor, request.UserId);
                
                // REQUERIMIENTO FASE 16: Comunicación desacoplada vía Bus
                await _bus.PublishAsync(new Victoria.Inventory.Domain.Events.DispatchConfirmed(
                    null, 
                    id, 
                    request.DockDoor, 
                    new List<string> { "LPN-GOLIVE-001" }, 
                    DateTime.UtcNow, 
                    request.UserId, 
                    "WEB-PORTAL"));

                return Ok(new { Message = "Order dispatched and event published to Bus.", LabelZPL = zpl });
            }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("receipt/approve-overage")]
        public async Task<IActionResult> ApproveReceiptOverage([FromBody] ApproveReceiptOverageRequest request)
        {
            try
            {
                await _approveReceiptOverageHandler.Handle(new ApproveReceiptOverageCommand
                {
                    TenantId = null,
                    LpnId = request.LpnId,
                    SupervisorId = request.SupervisorId
                });
                return Ok(new { Message = "Overage approved. LPN is now available for Putaway.", LpnId = request.LpnId });
            }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("count")]
        public async Task<IActionResult> ReportCount([FromBody] ReportCountRequest request)
        {
            try
            {
                await _countService.ProcessBlindCount(null, request.LpnId, request.CountedQuantity, request.UserId, request.StationId);
                return Ok(new { Message = "Count processed successfully.", LpnId = request.LpnId });
            }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("adjust")]
        public async Task<IActionResult> AuthorizeAdjustment([FromBody] AuthorizeAdjustmentRequest request)
        {
            try
            {
                await _countService.AuthorizeAdjustment(null, request.LpnId, request.NewQuantity, request.Reason, request.SupervisorId);
                return Ok(new { Message = "Adjustment authorized and completed.", LpnId = request.LpnId });
            }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }
    }

    public record ReceiveLpnRequest(string LpnId, string OrderId, int ExpectedQuantity, int ReceivedQuantity, string UserId, string StationId);
    public record PutawayLpnRequest(string LpnId, string LocationCode, string UserId, string StationId);
    public record AllocateOrderRequest(string OrderId, string Sku, int Quantity, string UserId, string StationId);
    public record PickLpnRequest(string LpnId, string UserId, string StationId);
    public record PackLpnsRequest(string MasterLpnId, List<string> ChildLpnIds, double Weight, string UserId, string StationId);
    public record DispatchOrderRequest(string DockDoor, string UserId);
    public record ReportCountRequest(string LpnId, int CountedQuantity, string UserId, string StationId);
    public record AuthorizeAdjustmentRequest(string LpnId, int NewQuantity, string Reason, string SupervisorId);
    public record ApproveReceiptOverageRequest(string LpnId, string SupervisorId);
}
