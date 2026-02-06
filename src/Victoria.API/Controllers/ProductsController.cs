using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/products")]
    public class ProductsController : ControllerBase
    {
        private readonly string _connectionString;

        public ProductsController(IConfiguration config)
        {
            _connectionString = config["POSTGRES_CONNECTION"] ?? "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] string tenantId)
        {
            var products = new List<Product>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT id, sku, name, tenantid, odooid, data FROM products WHERE tenantid = @tenant";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("tenant", tenantId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var product = new Product
                {
                    Id = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    Sku = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    TenantId = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    OdooId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                };
                products.Add(product);
            }

            return Ok(products);
        }
    }
}
