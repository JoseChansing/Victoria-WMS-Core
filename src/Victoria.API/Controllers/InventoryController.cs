using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Victoria.Inventory.Application.Commands;
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
        private readonly Victoria.Infrastructure.Integration.DispatchEventHandler _odooIntegration;

        public InventoryController(
            ReceiveLpnHandler receiveHandler, 
            PutawayLpnHandler putawayHandler,
            AllocateOrderHandler allocateHandler,
            PickLpnHandler pickHandler,
            PackingHandler packingHandler,
            Victoria.Inventory.Application.Services.DispatchService dispatchService,
            Victoria.Infrastructure.Integration.DispatchEventHandler odooIntegration)
        {
            _receiveHandler = receiveHandler;
            _putawayHandler = putawayHandler;
            _allocateHandler = allocateHandler;
            _pickHandler = pickHandler;
            _packingHandler = packingHandler;
            _dispatchService = dispatchService;
            _odooIntegration = odooIntegration;
        }

        [HttpPost("receipt")]
        public async Task<IActionResult> ReceiveLpn([FromBody] ReceiveLpnRequest request)
        {
            try
            {
                await _receiveHandler.Handle(new ReceiveLpnCommand
                {
                    TenantId = request.TenantId,
                    LpnId = request.LpnId,
                    OrderId = request.OrderId,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "LPN received successfully", LpnId = request.LpnId, Tenant = request.TenantId });
            }
            catch (TenantSecurityException ex) { return Forbid(ex.Message); }
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
                    TenantId = request.TenantId,
                    LpnId = request.LpnId,
                    LocationCode = request.LocationCode,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "Putaway completed successfully", LpnId = request.LpnId, Tenant = request.TenantId });
            }
            catch (TenantSecurityException ex) { return StatusCode(403, new { Error = ex.Message }); }
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
                    TenantId = request.TenantId,
                    OrderId = request.OrderId,
                    Sku = request.Sku,
                    Quantity = request.Quantity,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "Allocation successful", OrderId = request.OrderId, Tenant = request.TenantId });
            }
            catch (TenantSecurityException ex) { return StatusCode(403, new { Error = ex.Message }); }
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
                    TenantId = request.TenantId,
                    LpnId = request.LpnId,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "LPN picked successfully", LpnId = request.LpnId, Tenant = request.TenantId });
            }
            catch (TenantSecurityException ex) { return StatusCode(403, new { Error = ex.Message }); }
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
                    TenantId = request.TenantId,
                    MasterLpnId = request.MasterLpnId,
                    ChildLpnIds = request.ChildLpnIds,
                    Weight = request.Weight,
                    UserId = request.UserId,
                    StationId = request.StationId
                });
                return Ok(new { Message = "Packing completed. Master container created.", MasterLpnId = request.MasterLpnId, Tenant = request.TenantId });
            }
            catch (TenantSecurityException ex) { return StatusCode(403, new { Error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }

        [HttpPost("dispatch/{id}")]
        public async Task<IActionResult> DispatchOrder(string id, [FromBody] DispatchOrderRequest request)
        {
            try
            {
                var zpl = await _dispatchService.DispatchOrder(request.TenantId, id, request.DockDoor, request.UserId);
                
                // Simulación de disparo de integración con Odoo basado en el evento DispatchConfirmed
                _odooIntegration.Handle(new Victoria.Inventory.Domain.Events.DispatchConfirmed(request.TenantId, id, request.DockDoor, new List<string> { "LPN-TEST-001" }, DateTime.UtcNow, request.UserId, "API-INTERNAL"));

                return Ok(new { Message = "Order dispatched and notified to Odoo.", LabelZPL = zpl, Tenant = request.TenantId });
            }
            catch (TenantSecurityException ex) { return StatusCode(403, new { Error = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { Error = ex.Message }); }
        }
    }

    public record ReceiveLpnRequest(string TenantId, string LpnId, string OrderId, string UserId, string StationId);
    public record PutawayLpnRequest(string TenantId, string LpnId, string LocationCode, string UserId, string StationId);
    public record AllocateOrderRequest(string TenantId, string OrderId, string Sku, int Quantity, string UserId, string StationId);
    public record PickLpnRequest(string TenantId, string LpnId, string UserId, string StationId);
    public record PackLpnsRequest(string TenantId, string MasterLpnId, List<string> ChildLpnIds, double Weight, string UserId, string StationId);
    public record DispatchOrderRequest(string TenantId, string DockDoor, string UserId);
}
