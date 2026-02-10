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

using Victoria.Infrastructure.Integration.Odoo;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IDocumentSession _session; // Changed to IDocumentSession for Delete capability
        private readonly IOdooRpcClient _odooClient;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IDocumentSession session, IOdooRpcClient odooClient, ILogger<ProductsController> logger)
        {
            _session = session;
            _odooClient = odooClient;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null)
        {
            _logger.LogInformation($"[API] GetProducts request. Page: {page}, Size: {pageSize}, Search: '{search}'");
            try 
            {
                // Basic validation
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 1000) pageSize = 1000; // Cap max size

                QueryStatistics stats = null;
                var query = _session.Query<Product>().AsQueryable();

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

        [HttpDelete("{sku}")]
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
                var odooProducts = await _odooClient.SearchAndReadAsync<OdooProductDto>("product.product", domain, fields);

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
    }
}
