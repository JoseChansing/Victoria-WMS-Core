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

        public ProductsController(IQuerySession session)
        {
            _session = session;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _session.Query<Product>()
                .Take(100)
                .ToListAsync();

            return Ok(products);
        }
    }
}
