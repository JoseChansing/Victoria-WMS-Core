using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Victoria.Inventory.Domain.Events;
using Victoria.Inventory.Domain.Services;
using Victoria.Core;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/rfid")]
    public class RfidController : ControllerBase
    {
        private readonly IEpcParser _epcParser;
        private readonly IRfidDebouncer _debouncer;

        public RfidController(IEpcParser epcParser, IRfidDebouncer debouncer)
        {
            _epcParser = epcParser;
            _debouncer = debouncer;
        }

        [HttpPost("audit")]
        public IActionResult AuditInventory([FromBody] RfidAuditRequest request)
        {
            // Simulación de auditoría masiva
            var processedTags = new List<string>();
            var extraTags = new List<string>();
            var missingTags = new List<string>();

            // Supongamos que en la ubicación "LOC-001" esperamos estos tags (Hardcoded para el demo)
            var expectedEpcs = new List<string> { 
                "3074257BF400000000000001", 
                "3074257BF400000000000002" 
            };

            foreach (var epcHex in request.Epcs)
            {
                if (_debouncer.ShouldProcess(epcHex))
                {
                    try 
                    {
                        var epc = _epcParser.Parse(epcHex);
                        processedTags.Add(epcHex);
                        
                        if (!expectedEpcs.Contains(epcHex))
                            extraTags.Add(epcHex);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing EPC {epcHex}: {ex.Message}");
                    }
                }
            }

            missingTags = expectedEpcs.Except(request.Epcs).ToList();

            if (extraTags.Any() || missingTags.Any())
            {
                // Disparar evento RfidMismatchDetected (Simulado)
                var mismatchEvent = new RfidMismatchDetected(
                    null,
                    request.LocationCode,
                    missingTags,
                    extraTags,
                    DateTime.UtcNow,
                    "RFID-READER-AUTO",
                    "STATION-GATE-01"
                );
                
                return Ok(new 
                { 
                    Status = "MismatchDetected", 
                    Processed = processedTags.Count,
                    Missing = missingTags.Count, 
                    Extra = extraTags.Count,
                    Details = mismatchEvent
                });
            }

            return Ok(new { Status = "Clear", Processed = processedTags.Count });
        }

        [HttpPost("/simulate/rfid-burst")]
        public IActionResult SimulateBurst([FromBody] RfidBurstRequest request)
        {
            int accepted = 0;
            int rejected = 0;

            for (int i = 0; i < request.Iterations; i++)
            {
                foreach (var epc in request.Epcs)
                {
                    if (_debouncer.ShouldProcess(epc, request.WindowSeconds))
                        accepted++;
                    else
                        rejected++;
                }
            }

            return Ok(new 
            { 
                TotalReads = request.Iterations * request.Epcs.Count,
                Accepted = accepted, 
                Rejected = rejected,
                Efficiency = (double)rejected / (request.Iterations * request.Epcs.Count) * 100 + "%"
            });
        }
    }

    public record RfidAuditRequest(string LocationCode, List<string> Epcs);
    public record RfidBurstRequest(List<string> Epcs, int Iterations = 50, int WindowSeconds = 5);
}
