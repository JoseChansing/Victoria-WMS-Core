using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Marten;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IQuerySession _session;
        private readonly string _tenantId;

        public ProductsController(IQuerySession session, IConfiguration config)
        {
            _session = session;
            _tenantId = config["App:TenantId"] ?? "PERFECTPTY";
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _session.Query<Product>()
                .ToListAsync();

            return Ok(products);
        }
    }
}
