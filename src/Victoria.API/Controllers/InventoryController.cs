using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Victoria.Infrastructure.Projections;
using Victoria.Inventory.Application.Services;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly InventorySyncService _syncService;
        private readonly IDocumentSession _session;

        public InventoryController(InventorySyncService syncService, IDocumentSession session)
        {
            _syncService = syncService;
            _session = session;
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncInventory()
        {
            try
            {
                var count = await _syncService.SyncInventoryAsync();
                return Ok(new { message = "Inventory Sync Completed", imported_lpns = count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("items")]
        public async Task<ActionResult<IReadOnlyList<InventoryItemView>>> GetInventoryByItem()
        {
            var items = await _session.Query<InventoryItemView>().ToListAsync();
            return Ok(items);
        }

        [HttpGet("locations")]
        public async Task<ActionResult<IReadOnlyList<LocationInventoryView>>> GetAllLocationsInventory()
        {
            var views = await _session.Query<LocationInventoryView>().ToListAsync();
            return Ok(views);
        }

        [HttpGet("locations/{locationId}")]
        public async Task<ActionResult<LocationInventoryView>> GetInventoryByLocation(string locationId)
        {
            var view = await _session.LoadAsync<LocationInventoryView>(locationId);
            if (view == null)
            {
                return NotFound(new { message = $"No inventory found in location {locationId}" });
            }
            return Ok(view);
        }
        [HttpGet("by-location")]
        public async Task<ActionResult> GetInventoryByLocationReport()
        {
            var allLpns = await _session.Query<Lpn>().ToListAsync();
            
            // Fetch product names to include descriptions
            var skus = allLpns.Select(x => x.Sku.Value).Distinct().ToList();
            var products = await _session.Query<Product>()
                .Where(x => x.Sku.In(skus))
                .ToListAsync();
            // DEFENSIVE: Handle potential duplicate SKUs in master data
            var productMap = products.GroupBy(p => p.Sku).ToDictionary(g => g.Key, g => g.First().Name);

            var report = allLpns
                .Where(x => !string.IsNullOrEmpty(x.CurrentLocationId))
                .GroupBy(x => x.CurrentLocationId)
                .Select(g => new
                {
                    locationId = g.Key,
                    totalQty = g.Sum(x => x.Quantity),
                    lpnCount = g.Count(x => x.Status == LpnStatus.Active),
                    items = g.GroupBy(x => x.Sku.Value)
                             .Select(sg => new {
                                 sku = sg.Key,
                                 description = productMap.TryGetValue(sg.Key, out var name) ? name : "Sin descripción",
                                 quantity = sg.Sum(x => x.Quantity)
                             }).ToList()
                })
                .ToList();

            return Ok(report);
        }
    }
}
