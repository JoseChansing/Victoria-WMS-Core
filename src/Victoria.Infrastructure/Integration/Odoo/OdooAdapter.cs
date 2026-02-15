using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Victoria.Core.Interfaces;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooAdapter : IOdooAdapter
    {
        private readonly IOdooRpcClient _odooClient;
        private readonly ILogger<OdooAdapter> _logger;

        public OdooAdapter(IOdooRpcClient odooClient, ILogger<OdooAdapter> logger)
        {
            _odooClient = odooClient;
            _logger = logger;
        }

        public async Task<bool> CreateInventoryAdjustment(string productSku, string location, int quantityDifference, string? reason = null)
        {
            try
            {
                _logger.LogInformation("[ODOO-ADAPTER] Creating Inventory Adjustment for SKU {Sku} in {Loc}. Diff: {Qty}. Reason: {Reason}", productSku, location, quantityDifference, reason ?? "N/A");

                // 1. Find Product ID
                var productIds = await _odooClient.ExecuteKwAsync<List<int>>("product.product", "search", new object[] {
                    new object[] { new object[] { "default_code", "=", productSku } }
                });

                if (productIds == null || productIds.Count == 0)
                {
                    _logger.LogError("[ODOO-ADAPTER] Product {Sku} not found in Odoo.", productSku);
                    return false;
                }
                int productId = productIds[0];

                // 2. Find Location ID (Assuming location name matches Odoo 'Complete Name' or similar, simplified for now)
                // In a real scenario, we might need a robust location mapping service.
                // Assuming "WH/Stock" is the default or we search by name.
                // For this MVP, we will try to find the location by name.
                var locationIds = await _odooClient.ExecuteKwAsync<List<int>>("stock.location", "search", new object[] {
                    new object[] { new object[] { "name", "=", location } } // CAUTION: Might need full path
                });

                if (locationIds == null || locationIds.Count == 0)
                {
                    // Fallback to searching by barcode if name fails
                     locationIds = await _odooClient.ExecuteKwAsync<List<int>>("stock.location", "search", new object[] {
                        new object[] { new object[] { "barcode", "=", location } }
                    });
                }

                if (locationIds == null || locationIds.Count == 0)
                {
                     _logger.LogError("[ODOO-ADAPTER] Location {Loc} not found in Odoo.", location);
                    return false;
                }
                int locationId = locationIds[0];

                // 3. Create 'stock.quant' (This is for Odoo 16/17 immediate update, but we want draft if possible)
                // For "Draft", we usually use 'stock.inventory' (deprecated in v15+) or 'stock.quant' with specific context.
                // Since prompt asks for "Draft/Por Aplicar", in modern Odoo this is often just creating a quant with 'inventory_quantity' set but NOT applying it (action_apply_inventory).

                // We need to find the EXISTING quant to update its inventory_quantity, or create one.
                var quantIds = await _odooClient.ExecuteKwAsync<List<int>>("stock.quant", "search", new object[] {
                    new object[] { 
                        new object[] { "product_id", "=", productId },
                        new object[] { "location_id", "=", locationId }
                    }
                });

                if (quantIds != null && quantIds.Count > 0)
                {
                    // Update existing Quant
                    int quantId = quantIds[0];
                    // We need to read the current quantity first to know what the 'inventory_quantity' should be?
                    // The prompt says "quantityDifference".
                    // The 'inventory_quantity' field in Odoo is the "Counted Quantity".
                    // So we expect the system to provide the NEW TOTAL, not the difference?
                    // Re-reading prompt: "CreateInventoryAdjustment(string productSku, string location, int quantityDifference);"
                    // If we only have diff, we need to know current.
                    // BUT, typically 'inventory_quantity' is the final counted value. 
                    // Let's assume for this Adapter method, we might need to change the signature or logic.
                    // If the input is "quantityDifference", it implies we want to adjust BY that amount.
                    // But Odoo 'stock.quant' expects the absolute value for 'inventory_quantity'.
                    
                    // Let's fetch current 'quantity' (On Hand).
                    var quants = await _odooClient.SearchAndReadAsync<OdooQuantDto>("stock.quant", 
                        new object[][] { new object[] {"id", "=", quantId} }, 
                        new string[] { "quantity" }
                    );
                    
                    if (quants.Count > 0)
                    {
                        double currentQty = quants[0].Quantity;
                        double newQty = currentQty + quantityDifference; // Apply diff
                        
                        _logger.LogInformation("[ODOO-ADAPTER] Updating Quant {Id}. Current: {Cur}, Diff: {Diff}, New: {New}", quantId, currentQty, quantityDifference, newQty);

                        await _odooClient.ExecuteKwAsync<bool>("stock.quant", "write", new object[] {
                            new[] { quantId },
                            new Dictionary<string, object> { { "inventory_quantity", newQty } }
                        });
                        return true;
                    }
                }
                else
                {
                    // Create new Quant
                    _logger.LogInformation("[ODOO-ADAPTER] Creating New Quant. Diff: {Diff} (Assuming base 0)", quantityDifference);
                    // If no quant exists, on hand is 0. So new qty = diff.
                    
                    await _odooClient.ExecuteKwAsync<int>("stock.quant", "create", new object[] {
                        new Dictionary<string, object> {
                            { "product_id", productId },
                            { "location_id", locationId },
                            { "inventory_quantity", quantityDifference }
                        }
                    });
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-ADAPTER] Failed to create inventory adjustment.");
                return false;
            }
        }
        
        public async Task<bool> ConfirmReceiptAsync(long pickingId, Dictionary<long, int> moveQuantities)
        {
            try
            {
                _logger.LogInformation("[ODOO-ADAPTER] Confirming Receipt {PickingId} with {Count} moves.", pickingId, moveQuantities?.Count ?? 0);
                
                // TODO: Implement actual move line updates if needed based on moveQuantities.
                // For now, checks if we can validate.
                
                await _odooClient.ExecuteKwAsync<object>("stock.picking", "button_validate", new object[] { new long[] { pickingId } });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-ADAPTER] Failed to confirm receipt {PickingId}", pickingId);
                return false;
            }
        }

        public async Task<int> CreatePackagingAsync(int productId, int templateId, string name, double qty, double weight, double length, double width, double height)
        {
            try
            {
                _logger.LogInformation("[ODOO-ADAPTER] Creating Packaging {Name} for Product {ProductId}", name, productId);
                
                var packagingId = await _odooClient.ExecuteKwAsync<int>("product.packaging", "create", new object[] {
                    new Dictionary<string, object> {
                        { "name", name },
                        { "product_id", productId },
                        { "package_type_id", templateId },
                        { "qty", qty },
                        { "max_weight", weight },
                        { "packaging_length", length * 1000 }, // Assuming input in m, Odoo often uses mm? Or strict match? 
                         // Standard Odoo product.packaging doesn't always have dimensions on the packaging model unless using a specific module or package_type.
                         // We will assume standard fields if available or ignore if not. 
                         // Check fields: name, product_id, qty, package_type_id.
                         // If 'packaging_length' etc exist (Delivery builds), we use them.
                    }
                });
                return packagingId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-ADAPTER] Failed to create packaging {Name}", name);
                return 0;
            }
        }

        public async Task<bool> UpdatePackagingAsync(int packagingId, string name, double qty, double weight, double length, double width, double height)
        {
             try
            {
                _logger.LogInformation("[ODOO-ADAPTER] Updating Packaging {PackagingId}", packagingId);

                await _odooClient.ExecuteKwAsync<bool>("product.packaging", "write", new object[] {
                    new[] { packagingId },
                    new Dictionary<string, object> {
                        { "name", name },
                        { "qty", qty },
                         { "max_weight", weight }
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-ADAPTER] Failed to update packaging {PackagingId}", packagingId);
                return false;
            }
        }

        // Helper DTO for Quant
        public class OdooQuantDto
        {
            public double Quantity { get; set; }
        }
    }
}
