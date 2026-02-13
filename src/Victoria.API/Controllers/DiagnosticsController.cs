using Microsoft.AspNetCore.Mvc;
using Victoria.Core.Interfaces;
using System.Text.Json;
using Marten;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Core.Models;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IOdooRpcClient _odooClient;
        private readonly IDocumentSession _session;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(IOdooRpcClient odooClient, IDocumentSession session, ILogger<DiagnosticsController> logger)
        {
            _odooClient = odooClient;
            _session = session;
            _logger = logger;
        }

        [HttpGet("odoo-product/{sku}")]
        public async Task<IActionResult> DumpOdooProduct(string sku)
        {
            try
            {
                _logger.LogInformation($"[DIAGNOSTIC] Fetching raw Odoo data for SKU: {sku}");

                var domain = new object[][] 
                { 
                    new object[] { "default_code", "=", sku } 
                };

                var fields = new string[] 
                { 
                    "id",
                    "display_name",
                    "default_code",
                    "product_template_attribute_value_ids",
                    "product_template_variant_value_ids",
                    "attribute_line_ids",
                    "product_tmpl_id",
                    "name",
                    "barcode"
                };

                var result = await _odooClient.ExecuteKwAsync<List<Dictionary<string, object>>>(
                    "product.product",
                    "search_read",
                    new object[] { domain },
                    new Dictionary<string, object> { { "fields", fields } }
                );

                if (result == null || result.Count == 0)
                {
                    return NotFound(new { error = $"Product with SKU '{sku}' not found in Odoo" });
                }

                var product = result[0];
                var jsonOptions = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var jsonString = JsonSerializer.Serialize(product, jsonOptions);

                // Log to console
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine($"RAW ODOO DATA DUMP FOR SKU: {sku}");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine(jsonString);
                Console.WriteLine("═══════════════════════════════════════════════════════════");

                // Analyze attributes
                var analysis = new
                {
                    sku = sku,
                    odoo_id = product.ContainsKey("id") ? product["id"] : null,
                    display_name = product.ContainsKey("display_name") ? product["display_name"] : null,
                    raw_data = product,
                    attribute_analysis = AnalyzeAttributes(product)
                };

                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[DIAGNOSTIC] Error fetching Odoo data for SKU: {sku}");
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        private object AnalyzeAttributes(Dictionary<string, object> product)
        {
            var analysis = new Dictionary<string, object>();

            // Analyze product_template_attribute_value_ids
            if (product.ContainsKey("product_template_attribute_value_ids"))
            {
                var attrValues = product["product_template_attribute_value_ids"];
                analysis["product_template_attribute_value_ids"] = new
                {
                    type = attrValues?.GetType().Name ?? "null",
                    value = attrValues,
                    value_kind = attrValues is JsonElement je ? je.ValueKind.ToString() : "N/A",
                    is_array = attrValues is JsonElement jeArr && jeArr.ValueKind == JsonValueKind.Array,
                    array_length = attrValues is JsonElement jeLen && jeLen.ValueKind == JsonValueKind.Array ? jeLen.GetArrayLength() : 0
                };
            }

            // Analyze product_template_variant_value_ids
            if (product.ContainsKey("product_template_variant_value_ids"))
            {
                var variantValues = product["product_template_variant_value_ids"];
                analysis["product_template_variant_value_ids"] = new
                {
                    type = variantValues?.GetType().Name ?? "null",
                    value = variantValues,
                    value_kind = variantValues is JsonElement je ? je.ValueKind.ToString() : "N/A"
                };
            }

            // Analyze attribute_line_ids
            if (product.ContainsKey("attribute_line_ids"))
            {
                var attrLines = product["attribute_line_ids"];
                analysis["attribute_line_ids"] = new
                {
                    type = attrLines?.GetType().Name ?? "null",
                    value = attrLines,
                    value_kind = attrLines is JsonElement je ? je.ValueKind.ToString() : "N/A"
                };
            }

            // Analyze product_tmpl_id
            if (product.ContainsKey("product_tmpl_id"))
            {
                var tmplId = product["product_tmpl_id"];
                analysis["product_tmpl_id"] = new
                {
                    type = tmplId?.GetType().Name ?? "null",
                    value = tmplId,
                    value_kind = tmplId is JsonElement je ? je.ValueKind.ToString() : "N/A"
                };
            }

            return analysis;
        }
        [HttpGet("attribute/{id}")]
        public async Task<IActionResult> LookupAttribute(long id)
        {
            try
            {
                var domain = new object[][] { new object[] { "id", "=", id } };
                var fields = new string[] { "name", "attribute_id" };

                var templateValues = await _odooClient.ExecuteKwAsync<List<Dictionary<string, object>>>(
                    "product.template.attribute.value", "search_read", new object[] { domain }, new Dictionary<string, object> { { "fields", fields } });

                return Ok(new 
                { 
                    id = id,
                    template_values = templateValues
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("nuke-inbound")]
        public async Task<IActionResult> NukeInbound()
        {
            try
            {
                _logger.LogWarning("[RESTORE] NUCLEAR RESET INITIATED. Clearing all LPNs and Inbound data.");

                // 1. Delete all LPNs
                _session.DeleteWhere<Lpn>(x => true);

                // 2. Delete all Inbound Orders
                _session.DeleteWhere<InboundOrder>(x => true);

                // 3. Clear Sync States
                _session.DeleteWhere<SyncState>(x => x.Id == "InboundSync");
                _session.DeleteWhere<SyncState>(x => x.Id == "InboundOrderSync");

                await _session.SaveChangesAsync();

                return Ok(new { 
                    message = "Environment reset complete. Local inbound orders and LPNs cleared.", 
                    status = "Ready for fresh test"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-ERROR] Nuclear reset failed.");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet("odoo-pickings")]
        public async Task<IActionResult> InspectPickings()
        {
            try
            {
                // Verify Auth
                var uid = await _odooClient.AuthenticateAsync();
                
                var pickingDomain = new object[][] { };
                var pickingFields = new string[] { "name", "state", "picking_type_code", "write_date" };
                var pickings = await _odooClient.SearchAndReadAsync<dynamic>("stock.picking", pickingDomain, pickingFields, limit: 10);

                var productDomain = new object[][] { };
                var productFields = new string[] { "name", "default_code" };
                var products = await _odooClient.SearchAndReadAsync<dynamic>("product.product", productDomain, productFields, limit: 5);

                return Ok(new { 
                    Uid = uid,
                    Status = uid > 0 ? "Authenticated" : "Failed",
                    Pickings = pickings,
                    Products = products
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stack = ex.StackTrace });
            }
        }

    }
}
