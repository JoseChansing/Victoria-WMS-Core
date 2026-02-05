using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/inbound")]
    public class InboundController : ControllerBase
    {
        private readonly string _connectionString;

        public InboundController(IConfiguration config)
        {
            _connectionString = config["POSTGRES_CONNECTION"] ?? "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";
        }

        [HttpGet("kpis")]
        public async Task<IActionResult> GetKPIs([FromQuery] string tenantId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT COUNT(*) as Pending, SUM(TotalUnits) as Units FROM InboundOrders WHERE TenantId = @tenant AND Status = 'Pending'";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("tenant", tenantId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Ok(new
                {
                    PendingOrders = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]),
                    UnitsToReceive = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]),
                    HighPriorityCount = 0 // Mock por ahora
                });
            }

            return Ok(new { PendingOrders = 0, UnitsToReceive = 0, HighPriorityCount = 0 });
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] string tenantId)
        {
            var orders = new List<InboundOrder>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT Data FROM InboundOrders WHERE TenantId = @tenant ORDER BY Date DESC";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("tenant", tenantId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                var order = JsonSerializer.Deserialize<InboundOrder>(json);
                if (order != null) orders.Add(order);
            }

            return Ok(orders);
        }
    }
}
