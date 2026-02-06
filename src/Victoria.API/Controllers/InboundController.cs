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
    [Route("api/v1/inbound")]
    public class InboundController : ControllerBase
    {
        private readonly IQuerySession _session;

        public InboundController(IQuerySession session)
        {
            _session = session;
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

            return Ok(orders);
        }
    }
}
