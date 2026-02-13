using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Marten;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Victoria.Core.Interfaces;
using Victoria.Core.Models;
using Victoria.Infrastructure.Integration.Odoo;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/system")]
    public class SystemMaintenanceController : ControllerBase
    {
        private readonly IDocumentSession _session;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SystemMaintenanceController> _logger;
        private readonly IOdooRpcClient _odooClient;
        private readonly IOdooAdapter _odooAdapter;

        public SystemMaintenanceController(IDocumentSession session, IConfiguration configuration, ILogger<SystemMaintenanceController> logger, IOdooRpcClient odooClient, IOdooAdapter odooAdapter)
        {
            _session = session;
            _configuration = configuration;
            _logger = logger;
            _odooClient = odooClient;
            _odooAdapter = odooAdapter;
        }

        [HttpDelete("hard-reset-transactional")]
        public async Task<IActionResult> HardResetTransactional()
        {
            _logger.LogWarning("⚠️ [SYSTEM] INICIANDO HARD RESET TRANSACCIONAL...");

            // Hardcoded fallback for development convenience if config is missing, but prefer config.
            var connectionString = _configuration.GetConnectionString("Marten"); 
            if (string.IsNullOrEmpty(connectionString))
            {
                // Fallback attempt or error
                 connectionString = "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";
            }

            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                // 1. Purge Transactional Tables (JSON Documents)
                // Using DELETE instead of TRUNCATE to avoid potential lock escalations or foreign key strictness in Marten
                var sqlPurgeDocs = @"
                    DELETE FROM mt_doc_lpn;
                    DELETE FROM mt_doc_inboundorder;
                    DELETE FROM mt_doc_syncstate;
                    DELETE FROM mt_doc_location;
                ";

                using (var cmd = new NpgsqlCommand(sqlPurgeDocs, conn))
                {
                    cmd.CommandTimeout = 300; // 5 minutes
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("[SYSTEM] Documentos transaccionales eliminados.");
                }

                // 2. Purge Event Store (Streams & Events)
                try 
                {
                    var sqlPurgeEvents = @"
                        DELETE FROM mt_events;
                        DELETE FROM mt_streams;
                    ";

                    using (var cmd = new NpgsqlCommand(sqlPurgeEvents, conn))
                    {
                        cmd.CommandTimeout = 300;
                        await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("[SYSTEM] Event Store purgado por completo.");
                    }
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01") // UndefinedTable
                {
                    _logger.LogWarning("[SYSTEM] La tabla mt_events o mt_streams no existe. Saltando purgado de eventos.");
                    Console.WriteLine("[SYSTEM-WARN] Event tables do not exist. Skipping.");
                }

                return Ok(new 
                { 
                    Message = "HARD RESET COMPLETADO. Reinicia el servicio para aplicar cambios de código.",
                    Details = "Se eliminaron LPNs, Órdenes, Estados de Sincronización y TODO el historial de Eventos."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYSTEM] Error durante Hard Reset.");
                Console.WriteLine($"[SYSTEM-ERROR] {ex.Message}");
                Console.WriteLine($"[SYSTEM-ERROR] {ex.StackTrace}");
                // Return 200 OK with error details so I can see it in the response
                return Ok(new { Success = false, Error = ex.Message, StackTrace = ex.ToString() });
            }
        }

        [HttpGet("odoo-inspect/{sku}")]
        public async Task<IActionResult> InspectOdooProduct(string sku)
        {
            try
            {
                var fields = new string[] { "id", "display_name", "product_tmpl_id", "active" };
                var products = await _odooClient.SearchAndReadAsync<OdooProductDto>("product.product", 
                    new object[][] { new object[] { "default_code", "=", sku } }, fields);

                if (products == null || products.Count == 0)
                {
                    return NotFound(new { Message = $"Product with SKU {sku} not found in Odoo." });
                }

                return Ok(new { Product = products[0], RawData = products });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("test-packaging")]
        public async Task<IActionResult> TestPackaging([FromBody] dynamic request)
        {
            string sku = request.sku;
            double qty = request.qty ?? 1.0;
            
            try 
            {
                var product = await _session.LoadAsync<Product>(sku);
                if (product == null) return NotFound(new { error = "Local product not found" });

                _logger.LogInformation("[ODOO-TEST] Pushing test packaging for {Sku} - Variant: {VId}, Template: {TId}", sku, product.OdooId, product.OdooTemplateId);
                
                var odooId = await _odooAdapter.CreatePackagingAsync(product.OdooId, product.OdooTemplateId, "TEST-PKG", qty, 1.0, 1.0, 1.0, 1.0);
                
                return Ok(new { success = true, odooId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-TEST] Test packaging push failed.");
                return StatusCode(500, new { error = ex.Message, detail = ex.ToString() });
            }
        }
        [HttpGet("odoo-discover-bulk")]
        public async Task<IActionResult> DiscoverBulk()
        {
            try
            {
                // Try to find ANY record to see its structure
                var results = await _odooClient.SearchAndReadAsync<Dictionary<string, object>>("stock.move.bulk", 
                    new object[][] { }, new string[] { }, limit: 1);

                return Ok(new { Results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message, Detail = ex.ToString() });
            }
        }
    }
}
