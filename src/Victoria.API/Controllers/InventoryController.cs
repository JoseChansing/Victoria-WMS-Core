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
        public async Task<ActionResult> GetInventoryByItem()
        {
            var allLpns = await _session.Query<Lpn>().ToListAsync();

            if (!allLpns.Any()) return Ok(new List<object>());

            var skus = allLpns.Select(x => x.Sku.Value).Distinct().ToList();
            var products = await _session.Query<Product>()
                .Where(x => x.Sku.In(skus))
                .ToListAsync();
            
            var productMap = products.GroupBy(p => p.Sku).ToDictionary(g => g.Key, g => g.First().Name);

            var aggregatedItems = allLpns
                .Where(x => x.Status != LpnStatus.Consumed && x.Status != LpnStatus.Voided)
                .GroupBy(l => l.Sku.Value)
                .Select(g => new
                {
                    id = g.Key,
                    sku = g.Key,
                    description = productMap.ContainsKey(g.Key) ? productMap[g.Key] : "Provisional",
                    totalQuantity = g.Sum(l => l.Quantity),
                    primaryLocation = g.OrderByDescending(l => l.Quantity).FirstOrDefault()?.CurrentLocationId ?? "N/A",
                    lastUpdated = g.Max(l => l.CreatedAt) 
                })
                .OrderBy(x => x.sku)
                .ToList();

            return Ok(aggregatedItems);
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
                .Where(x => !string.IsNullOrEmpty(x.CurrentLocationId) && x.Status != LpnStatus.Consumed && x.Status != LpnStatus.Voided)
                .Select(lpn => new
                {
                    locationId = lpn.CurrentLocationId,
                    lpnId = lpn.Id,
                    sku = lpn.Sku.Value,
                    description = productMap.TryGetValue(lpn.Sku.Value, out var name) ? name : "Sin descripción",
                    quantity = lpn.Quantity,
                    allocatedQuantity = lpn.AllocatedQuantity,
                    status = (int)lpn.Status,
                    lpnType = lpn.Type.ToString(),
                    currentTaskId = lpn.CurrentTaskId
                })
                .ToList();

            return Ok(report);
        }

        [HttpGet("items/{sku}/lpns")]
        public async Task<ActionResult> GetItemLpns(string sku)
        {
            var lpns = await _session.Query<Lpn>()
                .Where(x => x.Sku.Value == sku && x.Status != LpnStatus.Consumed && x.Status != LpnStatus.Voided)
                .ToListAsync();

            var report = lpns
                .Select(lpn => new
                {
                    lpnId = lpn.Id,
                    locationId = lpn.CurrentLocationId ?? "N/A",
                    locationType = (lpn.CurrentLocationId ?? "").StartsWith("STAG") ? "Storage" : "Picking",
                    quantity = lpn.Quantity,
                    allocatedQuantity = lpn.AllocatedQuantity,
                    status = (int)lpn.Status,
                    currentTaskId = lpn.CurrentTaskId,
                    createdAt = lpn.CreatedAt
                })
                .OrderByDescending(x => x.createdAt)
                .ToList();

            return Ok(report);
        }
    }
}
