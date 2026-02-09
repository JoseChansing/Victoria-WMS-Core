using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/inventory/lpns")]
    public class LpnController : ControllerBase
    {
        private readonly IDocumentSession _session;

        public LpnController(IDocumentSession session)
        {
            _session = session;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<Lpn>>> GetAllLpns()
        {
            var lpns = await _session.Query<Lpn>().ToListAsync();
            return Ok(lpns);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Lpn>> GetLpn(string id)
        {
            var lpn = await _session.LoadAsync<Lpn>(id);
            if (lpn == null) return NotFound();
            return Ok(lpn);
        }

        [HttpGet("{id}/history")]
        public async Task<ActionResult<IEnumerable<object>>> GetLpnHistory(string id)
        {
            // Fetch raw events for this stream
            // Events.FetchStreamAsync returns IReadOnlyList<IEvent>
            var events = await _session.Events.FetchStreamAsync(id);
            
            if (events == null || !events.Any()) return NotFound(new { message = "No history found for this LPN" });

            // Map to simple structure for UI
            var history = events.Select(e => new 
            {
                EventId = e.Id,
                EventType = e.Data.GetType().Name,
                Data = e.Data,
                Timestamp = e.Timestamp,
                Version = e.Version
            });

            return Ok(history);
        }
    }
}
