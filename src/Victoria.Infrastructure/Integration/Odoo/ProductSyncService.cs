using System;
using Marten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Victoria.Core.Interfaces;
using System.Text.Json;
using System.Collections.Generic;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using System.Linq;

namespace Victoria.Infrastructure.Integration.Odoo
{
    // DTO Sucio de Odoo
    public class OdooProductDto
    {
        public int Id { get; set; }
        public int Company_Id { get; set; }
        public string Display_Name { get; set; } = string.Empty;
        public string Default_Code { get; set; } = string.Empty;
        public double Weight { get; set; }
        public string? Barcode { get; set; }
        public string? Description { get; set; }
        public string? Image_1920 { get; set; }
        public string? Image_128 { get; set; }
        public string? Type { get; set; } // Odoo v12-v14 compat (was detailed_type)
        public bool Active { get; set; }
        public object[]? Categ_Id { get; set; }
        public string Write_Date { get; set; } = string.Empty;
    }

    public class ProductSyncService
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<ProductSyncService> _logger;
        public ProductSyncService(IDocumentSession session, ILogger<ProductSyncService> logger)
        {
            _session = session;
            _logger = logger;
        }

        public async Task<int> SyncAllAsync(IOdooRpcClient odooClient)
        {
            _logger.LogInformation("[DELTA-SYNC] Syncing Products from Odoo...");
            
            // 1. Get Sync State
            var syncState = await _session.LoadAsync<SyncState>("ProductSync") ?? new SyncState { Id = "ProductSync", EntityType = "Product" };
            var domainList = new List<object[]> {
                new object[] { "active", "in", new bool[] { true, false } } // LIFECYCLE: Sync Archived
            };

            // new object[] { "active", "in", new object[] { true, false } }, // DEBUG
             // new object[] { "type", "in", new object[] { "product", "consu" } } // DEBUG

            if (syncState.LastSyncTimestamp != DateTime.MinValue)
            {
                var safeFilterDate = syncState.LastSyncTimestamp.AddMinutes(-15);
                domainList.Add(new object[] { "write_date", ">", safeFilterDate.ToString("yyyy-MM-dd HH:mm:ss") });
                Console.WriteLine($"🕒 [SYNC-PRODUCT] Buscando cambios desde {safeFilterDate} (Buffer -15m aplicado)");
            }

            var domain = domainList.ToArray();

            var fields = new string[] { 
                "id", "display_name", "default_code", "weight", "barcode", "description",
                "image_128", "image_1920", "type", "active", "write_date", "categ_id"
            };

            var odooProducts = await odooClient.SearchAndReadAsync<OdooProductDto>("product.product", domain, fields);
            
            if (odooProducts == null || odooProducts.Count == 0)
            {
                _logger.LogInformation("[DELTA-SYNC] No new or modified products found.");
                try { await System.IO.File.WriteAllTextAsync(@"C:\Users\orteg\OneDrive\Escritorio\Victoria WMS Core\PRODUCT_DEBUG.txt", "Found 0 products."); } catch {}
                return 0;
            }

            _logger.LogInformation($"[DELTA-SYNC] Processing {odooProducts.Count} modified products");
            try { await System.IO.File.WriteAllTextAsync(@"C:\Users\orteg\OneDrive\Escritorio\Victoria WMS Core\PRODUCT_DEBUG.txt", $"Found {odooProducts.Count} products."); } catch {}
            
            int processed = 0;
            foreach (var p in odooProducts)
            {
                await SyncProduct(p);
                processed++;
            }

            // 4. Update Sync State
            syncState.LastSyncTimestamp = DateTime.UtcNow;
            _session.Store(syncState);
            await _session.SaveChangesAsync();

            return processed;
        }

        public async Task SyncProduct(OdooProductDto odooProduct)
        {
            string skuCode = (odooProduct.Default_Code ?? "").ToUpper().Trim();

            // GUARD: Ignorar explícitamente basura conocida
            if (skuCode == "0" || odooProduct.Display_Name.Contains("Settle Invoice", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Odoo Sync] SKIPPING product '{odooProduct.Display_Name}' (Guard Match: {skuCode})");
                _logger.LogWarning("[FILTER-GUARD] Skipping system product '{Name}'", odooProduct.Display_Name);
                return;
            }

            if (string.IsNullOrEmpty(skuCode)) 
                skuCode = $"ODOO-{odooProduct.Id}";

            // LÓGICA DE IMAGEN (CASCADA)
            string imageSource = "null";
            string thumbnail = "";
            if (!string.IsNullOrEmpty(odooProduct.Image_1920)) 
            {
                imageSource = "variant";
                thumbnail = odooProduct.Image_128 ?? "";
            }
            else if (!string.IsNullOrEmpty(odooProduct.Image_128)) 
            {
                imageSource = "thumbnail";
                thumbnail = odooProduct.Image_128 ?? "";
            }

            // 1. Cargar producto existente (Upsert Pattern)
            var existingProduct = await _session.LoadAsync<Product>(skuCode);
            bool isNew = existingProduct == null;

            var product = existingProduct ?? new Product { Id = skuCode, Sku = skuCode };

            // 2. Mapeo Explícito
            product.Name = odooProduct.Display_Name;
            var rawDesc = odooProduct.Description ?? "";
            product.Description = (rawDesc == "0" || rawDesc == "false") ? "" : rawDesc;
            
            // Extract Category Name from many2one [id, name]
            if (odooProduct.Categ_Id != null && odooProduct.Categ_Id.Length >= 2)
            {
                product.Category = odooProduct.Categ_Id[1]?.ToString() ?? "";
            }

            product.Barcode = (odooProduct.Barcode == "0" || odooProduct.Barcode == "false" || string.IsNullOrEmpty(odooProduct.Barcode)) ? "" : odooProduct.Barcode;
            product.PhysicalAttributes = PhysicalAttributes.Create(odooProduct.Weight, 0, 0, 0);
            product.ImageSource = imageSource;
            product.Thumbnail = thumbnail;
            product.OdooId = odooProduct.Id;
            product.IsArchived = !odooProduct.Active;
            product.LastUpdated = DateTime.UtcNow; // UTC Check ✅

            // 3. Persistencia y Logs
            if (isNew)
            {
                Console.WriteLine($"Persistiendo producto: {product.Sku} - {product.Name} | Type: {odooProduct.Type}");
                _logger.LogInformation($"[ProductSync-Marten] Created new SKU '{skuCode}'");
            }
            else
            {
                Console.WriteLine($"🔄 [UPDATE] SKU: {product.Sku} | Archivado: {product.IsArchived}");
                Console.WriteLine($"♻️ [UPDATE] Producto actualizado: {product.Name}");
                _logger.LogInformation($"[ProductSync-Marten] Updated existing SKU '{skuCode}'");
            }

            _session.Store(product);
            await _session.SaveChangesAsync();
        }
    }
}
