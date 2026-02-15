using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Marten;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/locations")]
    public class LocationsController : ControllerBase
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<LocationsController> _logger;

        public LocationsController(IDocumentSession session, ILogger<LocationsController> logger)
        {
            _session = session;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _session.Query<Location>().ToListAsync();
            var lpnsInLocations = await _session.Query<Lpn>()
                .Where(x => x.CurrentLocationId != null)
                .ToListAsync();

            // Load all products for weight/volume calculation
            var skus = lpnsInLocations.Select(x => x.Sku.Value).Distinct().ToList();
            var products = await _session.Query<Product>()
                .Where(x => skus.Contains(x.Sku))
                .ToListAsync();
            var productMap = products.ToDictionary(p => p.Sku);

            var activeLpnMap = lpnsInLocations
                .Where(x => x.Status == LpnStatus.Available)
                .GroupBy(x => x.CurrentLocationId)
                .ToDictionary(g => g.Key!, g => g.Count());

            var unitCountMap = lpnsInLocations
                .GroupBy(x => x.CurrentLocationId)
                .ToDictionary(g => g.Key!, g => g.Sum(x => x.Quantity));

            // Calculate total weight and volume per location
            var weightMap = lpnsInLocations
                .GroupBy(x => x.CurrentLocationId)
                .ToDictionary(
                    g => g.Key!,
                    g => g.Sum(lpn => {
                        if (productMap.TryGetValue(lpn.Sku.Value, out var product))
                            return (double)(product.UnitWeight * lpn.Quantity);
                        return 0.0;
                    })
                );

            var volumeMap = lpnsInLocations
                .GroupBy(x => x.CurrentLocationId)
                .ToDictionary(
                    g => g.Key!,
                    g => g.Sum(lpn => {
                        if (productMap.TryGetValue(lpn.Sku.Value, out var product))
                            return (double)(product.UnitVolume * lpn.Quantity);
                        return 0.0;
                    })
                );

            var result = locations.Select(l => {
                var codeVal = (l.Code != null && !string.IsNullOrEmpty(l.Code.Value)) ? l.Code.Value : l.Id;
                var zoneVal = l.Code?.Zone ?? (l.Id.Contains("-") ? l.Id.Split('-')[0] : "MAIN");
                
                if (string.IsNullOrEmpty(codeVal)) {
                    codeVal = l.Id;
                }

                int displayCount;
                if (l.Profile == LocationProfile.Picking)
                {
                    displayCount = (codeVal != null && unitCountMap.TryGetValue(codeVal, out var q)) ? q : 0;
                }
                else
                {
                    displayCount = (codeVal != null && activeLpnMap.TryGetValue(codeVal, out var c)) ? c : 0;
                }

                var currentWeight = (codeVal != null && weightMap.TryGetValue(codeVal, out var w)) ? w : 0.0;
                var currentVolume = (codeVal != null && volumeMap.TryGetValue(codeVal, out var v)) ? v : 0.0;

                return new
                {
                    value = codeVal,
                    zone = zoneVal,
                    profile = l.Profile.ToString(),
                    status = l.Status.ToString(),
                    isPickable = l.IsPickable,
                    pickingSequence = l.PickingSequence,
                    maxWeight = l.MaxWeight,
                    maxVolume = l.MaxVolume,
                    currentWeight = Math.Round(currentWeight, 2),
                    currentVolume = Math.Round(currentVolume, 5),
                    barcode = string.IsNullOrEmpty(l.Barcode) ? codeVal : l.Barcode,
                    occupancyStatus = GetOccupancyStatus(l, displayCount),
                    lpnCount = displayCount
                };
            });

            return Ok(result);
        }

        private string GetOccupancyStatus(Location loc, int count)
        {
            if (count == 0) return "Empty";
            
            if (loc.Profile == LocationProfile.Reserve && count >= 1) return "Full";
            
            return "Partial";
        }

        [HttpPost]
        public async Task<IActionResult> CreateLocation([FromBody] LocationImportDto dto)
        {
            var existing = await _session.Query<Location>()
                .Where(x => x.Code.Value == dto.Code)
                .FirstOrDefaultAsync();

            if (existing != null)
                return BadRequest(new { Error = "La ubicación ya existe." });

            var newLoc = Location.Create(
                LocationCode.Create(dto.Code), 
                Enum.Parse<LocationProfile>(dto.Profile), 
                dto.IsPickable
            );
            
            newLoc.UpdateMetadata(
                dto.PickingSequence, 
                dto.MaxWeight, 
                dto.MaxVolume, 
                dto.Barcode ?? dto.Code, 
                "SYS", 
                "WEB-API"
            );
            
            _session.Store(newLoc);
            await _session.SaveChangesAsync();

            return Ok(newLoc);
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportLocations([FromBody] List<LocationImportDto> importData)
        {
            foreach (var dto in importData)
            {
                var existing = await _session.Query<Location>()
                    .Where(x => x.Code.Value == dto.Code)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    existing.UpdateMetadata(dto.PickingSequence, dto.MaxWeight, dto.MaxVolume, dto.Barcode ?? dto.Code, "IMPORT", "WEB-IMPORT");
                    _session.Store(existing);
                }
                else
                {
                    var newLoc = Location.Create(
                        LocationCode.Create(dto.Code), 
                        Enum.Parse<LocationProfile>(dto.Profile), 
                        dto.IsPickable
                    );
                    newLoc.UpdateMetadata(dto.PickingSequence, dto.MaxWeight, dto.MaxVolume, dto.Barcode ?? dto.Code, "IMPORT", "WEB-IMPORT");
                    _session.Store(newLoc);
                }
            }

            await _session.SaveChangesAsync();
            return Ok(new { Message = $"Imported {importData.Count} locations" });
        }

        [HttpDelete("{code}")]
        public async Task<IActionResult> DeleteLocation(string code)
        {
            var hasInventory = await _session.Query<Lpn>().AnyAsync(x => x.CurrentLocationId == code);
            if (hasInventory)
            {
                return BadRequest(new { Error = "Ubicación con inventario. Mover antes de borrar." });
            }

            _session.DeleteWhere<Location>(x => x.Code.Value == code);
            await _session.SaveChangesAsync();

            return Ok(new { Message = $"Location {code} deleted" });
        }

        [HttpPut("{code}")]
        public async Task<IActionResult> UpdateLocation(string code, [FromBody] LocationUpdateDto dto)
        {
            var loc = await _session.Query<Location>().Where(x => x.Code.Value == code).FirstOrDefaultAsync();
            if (loc == null) return NotFound();

            loc.UpdateMetadata(dto.PickingSequence, dto.MaxWeight, dto.MaxVolume, dto.Barcode, "SYS", "WEB-API");
            _session.Store(loc);
            await _session.SaveChangesAsync();

            return Ok(loc);
        }

        [HttpPut("{code}/profile")]
        public async Task<IActionResult> UpdateLocationProfile(string code, [FromBody] ProfileUpdateDto dto)
        {
            var loc = await _session.Query<Location>().Where(x => x.Code.Value == code).FirstOrDefaultAsync();
            if (loc == null) return NotFound();

            // Update profile and isPickable directly
            loc.Profile = Enum.Parse<LocationProfile>(dto.Profile);
            loc.IsPickable = dto.IsPickable;
            
            _session.Store(loc);
            await _session.SaveChangesAsync();

            return Ok(new { Message = $"Profile updated to {dto.Profile}" });
        }

        [HttpPost("create-masters")]
        public async Task<IActionResult> CreateMasterLocations()
        {
            var masterCodes = new[] { "DOCK-LPN", "STAGE-RESERVE", "STAGE-PICKING", "PHOTO-STATION" };
            var created = 0;

            foreach (var code in masterCodes)
            {
                var existing = await _session.Query<Location>()
                    .FirstOrDefaultAsync(x => x.Code.Value == code);

                if (existing == null)
                {
                    var location = Location.Create(
                        LocationCode.Create(code),
                        LocationProfile.Reserve,
                        false
                    );
                    location.UpdateMetadata(0, 10000.0, 100.0, code, "SETUP", "API");
                    _session.Store(location);
                    created++;
                    _logger.LogInformation("Created master location: {Code}", code);
                }
            }

            await _session.SaveChangesAsync();
            return Ok(new { Message = $"Created {created} master locations" });
        }
    }

    public class LocationImportDto
    {
        public string Code { get; set; } = string.Empty;
        public string Profile { get; set; } = "Picking";
        public bool IsPickable { get; set; } = true;
        public int PickingSequence { get; set; }
        public double MaxWeight { get; set; }
        public double MaxVolume { get; set; }
        public string? Barcode { get; set; }
    }

    public class LocationUpdateDto
    {
        public int PickingSequence { get; set; }
        public double MaxWeight { get; set; }
        public double MaxVolume { get; set; }
        public string Barcode { get; set; } = string.Empty;
    }

    public class ProfileUpdateDto
    {
        public string Profile { get; set; } = "Reserve";
        public bool IsPickable { get; set; }
    }
}
