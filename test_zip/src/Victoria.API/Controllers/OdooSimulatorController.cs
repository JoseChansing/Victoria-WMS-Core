using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Victoria.Infrastructure.Integration.Odoo;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("simulate/odoo")]
    public class OdooSimulatorController : ControllerBase
    {
        private readonly ProductSyncService _productSync;
        private readonly InboundOrderSyncService _orderSync;

        public OdooSimulatorController(ProductSyncService productSync, InboundOrderSyncService orderSync)
        {
            _productSync = productSync;
            _orderSync = orderSync;
        }

        [HttpPost("sync-product")]
        public async Task<IActionResult> SyncProduct([FromBody] OdooProductDto product)
        {
            await _productSync.SyncProduct(product);
            return Ok(new { Message = "Product integrated via ACL", Sku = product.Default_Code });
        }

        [HttpPost("sync-order")]
        public async Task<IActionResult> SyncOrder([FromBody] OdooOrderDto order)
        {
            await _orderSync.SyncPicking(order, "incoming");
            return Ok(new { Message = "Order integrated and lines deduplicated", OrderId = order.Name });
        }
    }
}
