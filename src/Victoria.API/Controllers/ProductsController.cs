using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Marten;
using Marten.Linq;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Core.Interfaces;
using Victoria.Core.Models;

using Victoria.Infrastructure.Integration.Odoo;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IDocumentSession _session; 
        private readonly IOdooRpcClient _odooClient;
        private readonly IProductService _productSync;
        private readonly IInboundService _inboundSync;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IDocumentSession session, 
            IOdooRpcClient odooClient, 
            IProductService productSync,
            IInboundService inboundSync,
            ILogger<ProductsController> logger)
        {
            _session = session;
            _odooClient = odooClient;
            _productSync = productSync;
            _inboundSync = inboundSync;
            _logger = logger;
        }


        [HttpGet]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? brand = null,
            [FromQuery] string? category = null,
            [FromQuery] bool? hasImage = null)
        {
            _logger.LogInformation($"[API] GetProducts request. Page: {page}, Size: {pageSize}, Filter: B:{brand} C:{category}");
            try 
            {
                // Basic validation
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 1000) pageSize = 1000; // Cap max size

                QueryStatistics stats = null;
                var query = _session.Query<Product>().AsQueryable();

                // Apply filters (Free Text Search)
                if (!string.IsNullOrWhiteSpace(brand))
                    query = query.Where(x => x.Brand.Contains(brand, StringComparison.InvariantCultureIgnoreCase));

                if (!string.IsNullOrWhiteSpace(category))
                    query = query.Where(x => x.Category.Contains(category, StringComparison.InvariantCultureIgnoreCase));

                if (hasImage.HasValue)
                    query = query.Where(x => x.HasImage == hasImage.Value);

                // Apply search filter if provided
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.Trim();
                    if (searchTerm.Contains(","))
                    {
                        var skus = searchTerm.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => s.Trim().ToUpper())
                                             .ToList();
                        
                        query = query.Where(x => x.Sku.In(skus));
                    }
                    else
                    {
                        // Case-insensitive filtering via Marten
                        query = query.Where(x => 
                            x.Sku.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) || 
                            x.Name.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                            x.Barcode.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase));
                    }
                }

                var products = await query
                    .Stats(out stats)
                    .OrderBy(x => x.Sku) // Default sort
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new Victoria.Core.Models.PagedResult<Product>(
                    products, 
                    stats.TotalResults, 
                    page, 
                    pageSize);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] GetProducts failed.");
                return StatusCode(500, new { error = $"Error al obtener productos: {ex.Message}", details = ex.ToString() });
            }
        }



        // Constraint :minlength(5) ensures 'meta' (4 chars) is never caught here
        [HttpDelete("{sku:minlength(5)}")]
        public async Task<IActionResult> DeleteProduct(string sku)
        {
            // 1. Validación Local: Movimientos de Inventario
            // Usamos Query<Lpn> para ver si hay algún LPN asociado a este SKU
            var hasInventory = await _session.Query<Lpn>().AnyAsync(x => x.Sku.Value == sku);
            if (hasInventory)
            {
                return BadRequest(new { error = "No se puede eliminar: El producto tiene movimientos de inventario asociados." });
            }

            // 2. Validación Remota: Odoo
            // Buscamos si el producto existe en Odoo (incluso si está archivado, existe)
            try 
            {
                Console.WriteLine($"🔍 Validando existencia en Odoo para SKU: '{sku}'...");
                
                // CRITICAL FIX: Improved Odoo Search
                // 1. Search by default_code exactly.
                // 2. Include active=True AND active=False to find EVERYTHING.
                var domain = new object[][] { 
                    new object[] { "default_code", "=", sku },
                    new object[] { "active", "in", new bool[] { true, false } } 
                };
                
                var fields = new string[] { "id", "display_name", "active" }; // Request 'active' for debugging if needed
                var odooProducts = await _odooClient.SearchAndReadAsync<Victoria.Core.Models.OdooProductDto>("product.product", domain, fields);

                if (odooProducts != null && odooProducts.Count > 0)
                {
                    // Found it! Block deletion.
                    var p = odooProducts[0];
                    return Conflict(new { error = $"El producto aún existe en Odoo ({p.Display_Name} - Activo: {p.Active}). Elimínelo allá primero o desvincúlelo." });
                }
            }
            catch (Exception ex)
            {
                // Si falla Odoo, ¿bloqueamos? Por seguridad, sí.
                return StatusCode(500, new { error = $"Error validando en Odoo: {ex.Message}" });
            }

            // 3. Ejecución
            _session.DeleteWhere<Product>(x => x.Sku == sku);
            await _session.SaveChangesAsync();

            return Ok(new { message = $"Producto {sku} eliminado correctamente." });
        }

        [HttpPost("{sku}/packaging")]
        public async Task<IActionResult> CreatePackaging(string sku, [FromBody] PackagingRequest request)
        {
            try 
            {
                var product = await _session.LoadAsync<Product>(sku);
                if (product == null) return NotFound(new { error = "Producto no encontrado localmente." });

                var values = new Dictionary<string, object>
                {
                    { "name", request.Name },
                    { "qty_bulk", (double)request.Qty },
                    { "product_ids", new object[] { new object[] { 6, 0, new object[] { product.OdooId } } } }, // Many2Many relation
                    { "l_cm", (double)request.Length },
                    { "w_cm", (double)request.Width },
                    { "h_cm", (double)request.Height },
                    { "weight", (double)request.Weight }
                };

                var result = await _odooClient.ExecuteActionAsync("stock.move.bulk", "create", new object[] { values });
                if (result == null) return BadRequest(new { error = "Error al crear empaque en Odoo." });

                int odooId = int.TryParse(result.ToString(), out var id) ? id : 0;

                await _productSync.SyncSingleAsync(_odooClient, sku);
                return Ok(new { message = "Empaque creado correctamente.", odooId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] CreatePackaging failed.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("{sku}/packaging/{odooId}")]
        public async Task<IActionResult> UpdatePackaging(string sku, int odooId, [FromBody] PackagingRequest request)
        {
            try 
            {
                var values = new Dictionary<string, object>
                {
                    { "name", request.Name },
                    { "qty_bulk", (double)request.Qty },
                    { "l_cm", (double)request.Length },
                    { "w_cm", (double)request.Width },
                    { "h_cm", (double)request.Height },
                    { "weight", (double)request.Weight }
                };

                // Odoo Write expects ([ids], values)
                var success = await _odooClient.ExecuteAsync("stock.move.bulk", "write", new object[] { new object[] { odooId }, values });
                if (!success) return BadRequest(new { error = "Error al actualizar empaque en Odoo o no se detectaron cambios." });

                await _productSync.SyncSingleAsync(_odooClient, sku);
                return Ok(new { message = "Empaque actualizado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] UpdatePackaging failed.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("{sku}/packaging/{odooId}")]
        public async Task<IActionResult> DeletePackaging(string sku, int odooId)
        {
            try 
            {
                // Odoo Unlink expects ([ids])
                var success = await _odooClient.ExecuteAsync("stock.move.bulk", "unlink", new object[] { new object[] { odooId } });
                if (!success) return BadRequest(new { error = "Error al eliminar empaque en Odoo." });

                await _productSync.SyncSingleAsync(_odooClient, sku);
                return Ok(new { message = "Empaque eliminado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] DeletePackaging failed.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("sync/force")]
        public async Task<IActionResult> ForceFullSync()
        {
            try
            {
                _logger.LogWarning("[MANUAL-TRIGGER] Forcing FULL Product Sync requested via API.");
                
                // 1. Reset Sync State
                var syncState = await _session.LoadAsync<SyncState>("ProductSync");
                if (syncState != null)
                {
                    syncState.LastSyncTimestamp = DateTime.MinValue; // Force logic in Service to treat it as new
                    _session.Store(syncState);
                    await _session.SaveChangesAsync();
                }

                // 2. Trigger Sync
                var count = await _productSync.SyncAllAsync(_odooClient);
                return Ok(new { message = $"Sincronización masiva completada. Productos procesados: {count}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] ForceFullSync failed.");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class PackagingRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal Weight { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
    }
}
