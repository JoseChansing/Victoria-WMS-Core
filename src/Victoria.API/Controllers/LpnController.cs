using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Infrastructure.Projections;

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
            var lpns = await _session.Query<Lpn>()
                .Where(x => x.Status == LpnStatus.Active)
                .ToListAsync();
            return Ok(lpns);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<LpnDetailView>> GetLpnDetail(string id)
        {
            var detail = await _session.LoadAsync<LpnDetailView>(id);
            if (detail == null) return NotFound();
            return Ok(detail);
        }

        [HttpGet("{id}/history")]
        public async Task<ActionResult<LpnHistoryView>> GetLpnHistory(string id)
        {
            var history = await _session.LoadAsync<LpnHistoryView>(id);
            if (history == null)
            {
                // Devolvemos un objeto vacío en lugar de 404 para que la UI no falle
                return Ok(new LpnHistoryView { Id = id, Entries = new List<LpnHistoryEntry>() });
            }
            return Ok(history);
        }
    }
}
