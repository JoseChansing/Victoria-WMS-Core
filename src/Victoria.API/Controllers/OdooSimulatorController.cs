using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Victoria.Infrastructure.Integration.Odoo;
using Victoria.Core.Interfaces;
using Victoria.Core.Models;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("simulate/odoo")]
    public class OdooSimulatorController : ControllerBase
    {
        private readonly IProductService _productSync;
        private readonly IInboundService _orderSync;

        public OdooSimulatorController(IProductService productSync, IInboundService orderSync)
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
