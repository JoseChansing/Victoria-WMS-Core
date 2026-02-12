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
using System.Text.Json.Serialization;
using Victoria.Core.Models;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class ProductSyncService : IProductService
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
            // 1. Get Sync State
            var syncState = await _session.LoadAsync<SyncState>("ProductSync") ?? new SyncState { Id = "ProductSync", EntityType = "Product" };
            var domainList = new List<object[]> {
                new object[] { "active", "=", true },
                new object[] { "categ_id.name", "in", new string[] { 
                    "AMORTIGUADORES", "ANILLOS DE PISTON", "ELECTROVENTILADORES", 
                    "SOPORTES DE MOTOR", "MULETAS", "RADIADORES", "CONDENSADORES",
                    "PUNTAS DE FLECHA", "BASES DE AMORTIGUADOR"
                } }
            };

            // INCREMENTAL SYNC LOGIC
            if (syncState.LastSyncTimestamp != DateTime.MinValue)
            {
                // Safety margin: 15 minutes overlap to avoid clock skew issues
                var safeTimestamp = syncState.LastSyncTimestamp.AddMinutes(-15);
                var odooDateFormat = safeTimestamp.ToString("yyyy-MM-dd HH:mm:ss");
                
                domainList.Add(new object[] { "write_date", ">=", odooDateFormat });
                _logger.LogInformation($"[INCREMENTAL-SYNC] Fetching products modified since {odooDateFormat} (Safety Margin 15m applied)");
            }
            else
            {
                _logger.LogInformation("[FULL-SYNC] No previous sync timestamp found. Executing full load.");
            }

            var domain = domainList.ToArray();

            var fields = new string[] { 
                "id", "display_name", "default_code", "weight", "barcode", "description",
                "image_128", "image_1920", "type", "active", "write_date", "categ_id",
                "brand_id", "product_template_attribute_value_ids", "product_template_variant_value_ids",
                "bulk_ids"
            };

            int batchSize = 50;
            int offset = 0;
            int totalProcessed = 0;
            bool hasMore = true;

            //_logger.LogInformation("[MASSIVE-SYNC] Starting massive synchronization for 20,000+ products (Batches of 100)...");

            while (hasMore)
            {
                await Task.Delay(1000); // BREATH: avoid overwhelming Odoo
                _logger.LogInformation($"[MASSIVE-SYNC] Fetching products offset: {offset}, limit: {batchSize}...");
                var odooProducts = await odooClient.SearchAndReadAsync<OdooProductDto>("product.product", domain, fields, limit: batchSize, offset: offset);

                if (odooProducts == null || odooProducts.Count == 0)
                {
                    hasMore = false;
                    break;
                }

                _logger.LogInformation($"[MASSIVE-SYNC] Processing batch of {odooProducts.Count} products...");

                // 2. Extract all attribute value IDs for this batch
                var allAttributeValueIds = new HashSet<long>();
                foreach (var p in odooProducts)
                {
                    ExtractAttributeIds(p.product_template_attribute_value_ids, allAttributeValueIds);
                    ExtractAttributeIds(p.product_template_variant_value_ids, allAttributeValueIds);
                }

                // 3. Fetch Attribute Details for this batch
                var attributeValueMap = new Dictionary<long, (string AttributeName, string ValueName)>();
                if (allAttributeValueIds.Count > 0)
                {
                    var attrIds = allAttributeValueIds.Select(id => (object)id).ToArray();
                    var attrDomain = new object[][] { new object[] { "id", "in", attrIds } };
                    var attrFields = new string[] { "name", "attribute_id" };

                    try 
                    {
                        var attrValues = await odooClient.ExecuteKwAsync<List<Dictionary<string, object>>>(
                            "product.template.attribute.value", 
                            "search_read", 
                            new object[] { attrDomain }, 
                            new Dictionary<string, object> { { "fields", attrFields } }
                        );

                        if (attrValues != null)
                        {
                            foreach (var val in attrValues)
                            {
                                if (val.TryGetValue("id", out var idObj) && 
                                    val.TryGetValue("name", out var nameObj) && 
                                    val.TryGetValue("attribute_id", out var attrObj))
                                {
                                    long id = 0;
                                    if (idObj is JsonElement idEl && idEl.ValueKind == JsonValueKind.Number) id = idEl.GetInt64();
                                    else if (long.TryParse(idObj?.ToString(), out var lVal)) id = lVal;

                                    string valueName = nameObj?.ToString() ?? "";
                                    string attrName = "";

                                    if (attrObj is System.Collections.IEnumerable enumerable && !(attrObj is string))
                                    {
                                        var list = new List<object>();
                                        foreach (var item in enumerable) list.Add(item);
                                        if (list.Count > 1) attrName = list[1]?.ToString() ?? "";
                                    }
                                    else if (attrObj is JsonElement ae && ae.ValueKind == JsonValueKind.Array && ae.GetArrayLength() > 1)
                                    {
                                        attrName = ae[1].ToString();
                                    }
                                    
                                    attributeValueMap[id] = (attrName, valueName);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[MASSIVE-SYNC] Error fetching attributes for batch.");
                    }
                }

                // 3.5 Fetch Packaging Details for this batch
                var allPackagingIds = new HashSet<long>();
                var allBulkIds = new HashSet<long>();
                foreach (var p in odooProducts)
                {
                    ExtractAttributeIds(p.packaging_ids, allPackagingIds);
                    ExtractAttributeIds(p.bulk_ids, allBulkIds);
                }

                var packagingMap = new Dictionary<long, OdooPackagingDto>();
                
                // Fetch Standard Packaging
                if (allPackagingIds.Count > 0)
                {
                    var packIds = allPackagingIds.Select(id => (object)id).ToArray();
                    var packDomain = new object[][] { new object[] { "id", "in", packIds } };
                    var packFields = new string[] { "id", "name", "qty", "packaging_length", "packaging_width", "packaging_height", "max_weight" };

                    try 
                    {
                        var packings = await odooClient.SearchAndReadAsync<OdooPackagingDto>("product.packaging", packDomain, packFields);
                        if (packings != null)
                        {
                            foreach (var pkg in packings) packagingMap[pkg.Id] = pkg;
                        }
                    }
                    catch (Exception ex) { _logger.LogError(ex, "[MASSIVE-SYNC] Error fetching packaging for batch."); }
                }

                // Fetch Custom Bultos (stock.move.bulk)
                if (allBulkIds.Count > 0)
                {
                    var bulkIds = allBulkIds.Select(id => (object)id).ToArray();
                    var bulkDomain = new object[][] { new object[] { "id", "in", bulkIds } };
                    var bulkFields = new string[] { "id", "name", "qty_bulk", "l_cm", "w_cm", "h_cm", "weight" };

                    try 
                    {
                        var bulks = await odooClient.SearchAndReadAsync<OdooPackagingDto>("stock.move.bulk", bulkDomain, bulkFields);
                        if (bulks != null)
                        {
                            foreach (var b in bulks) packagingMap[b.Id] = b;
                        }
                    }
                    catch (Exception ex) { _logger.LogError(ex, "[MASSIVE-SYNC] Error fetching bulk_ids for batch."); }
                }

                // 4. Update products in this batch
                foreach (var p in odooProducts)
                {
                    try 
                    {
                        await SyncProduct(p, attributeValueMap, packagingMap);
                        totalProcessed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[MASSIVE-SYNC] Error processing SKU {Sku} (OdooId: {Id}). Skipping product.", p.Default_Code, p.Id);
                    }
                }

                // Save changes for this batch to avoid keeping 20k objects in memory
                await _session.SaveChangesAsync();
                _logger.LogInformation($"[MASSIVE-SYNC] Batch completed. Total so far: {totalProcessed}");

                offset += batchSize;
                if (odooProducts.Count < batchSize) hasMore = false;
            }

            // 5. Final Sync State Update
            syncState.LastSyncTimestamp = DateTime.UtcNow;
            _session.Store(syncState);
            await _session.SaveChangesAsync();

            _logger.LogInformation($"[MASSIVE-SYNC] Massive sync completed. Total processed: {totalProcessed}");
            return totalProcessed;
        }

        private void ExtractAttributeIds(object? attrData, HashSet<long> targetSet)
        {
            if (attrData == null) return;

            if (attrData is JsonElement elem)
            {
                if (elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Number) targetSet.Add(item.GetInt64());
                        else if (long.TryParse(item.ToString(), out var lVal)) targetSet.Add(lVal);
                    }
                }
                else if (elem.ValueKind == JsonValueKind.Number)
                {
                    targetSet.Add(elem.GetInt64());
                }
                else if (elem.ValueKind == JsonValueKind.String)
                {
                    ExtractAttributeIds(elem.GetString(), targetSet);
                }
            }
            else if (attrData is System.Collections.IEnumerable enumData && !(attrData is string))
            {
                foreach (var item in enumData)
                    if (long.TryParse(item?.ToString(), out var lVal)) targetSet.Add(lVal);
            }
            else 
            {
                string raw = attrData.ToString() ?? "";
                if (string.IsNullOrEmpty(raw) || raw == "false" || raw == "0") return;

                var parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                    if (long.TryParse(part, out var lVal)) targetSet.Add(lVal);
            }
        }

        public async Task SyncProduct(OdooProductDto odooProduct)
        {
            await SyncProduct(odooProduct, null, null);
        }

        public async Task SyncProduct(OdooProductDto odooProduct, Dictionary<long, (string AttributeName, string ValueName)>? attributeMap, Dictionary<long, OdooPackagingDto>? packagingMap)
        {
            string skuCode = (odooProduct.Default_Code ?? "").ToUpper().Trim();

            // GUARD: Ignorar explícitamente basura conocida o nulos
            if (string.IsNullOrEmpty(skuCode) && odooProduct.Id == 0) return;
            
            string displayName = odooProduct.Display_Name ?? "Unnamed Product";

            if (skuCode == "0" || displayName.Contains("Settle Invoice", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[FILTER-GUARD] Skipping system product '{Name}'", displayName);
                return;
            }

            if (string.IsNullOrEmpty(skuCode)) 
                skuCode = $"ODOO-{odooProduct.Id}";

            Func<string?, bool> isValidImage = s => !string.IsNullOrEmpty(s) && s != "false" && s != "0";
            string imageSource = isValidImage(odooProduct.Image_1920) ? "variant" : (isValidImage(odooProduct.Image_128) ? "thumbnail" : "null");

            var existingProduct = await _session.LoadAsync<Product>(skuCode);
            var product = existingProduct ?? new Product { Id = skuCode, Sku = skuCode };

            product.Name = odooProduct.Display_Name;
            var rawDesc = odooProduct.Description ?? "";
            product.Description = (rawDesc == "0" || rawDesc == "false") ? "" : rawDesc;
            
            // Mapeo Categoría
            string categoryName = "";
            try {
                if (odooProduct.Categ_Id is System.Collections.IEnumerable enumerable && !(odooProduct.Categ_Id is string))
                {
                    var list = new List<object>();
                    foreach (var item in enumerable) list.Add(item);
                    if (list.Count > 1) categoryName = list[1]?.ToString() ?? "";
                }
                else if (odooProduct.Categ_Id is JsonElement cje && cje.ValueKind == JsonValueKind.Array && cje.GetArrayLength() > 1)
                    categoryName = cje[1].GetString() ?? "";
                else if (odooProduct.Categ_Id is string cs)
                    categoryName = cs;
            } catch {}
            product.Category = categoryName;

            // Mapeo Brand
            string brandName = "";
            try {
                if (odooProduct.brand_id is JsonElement je && je.ValueKind == JsonValueKind.Array && je.GetArrayLength() > 1)
                    brandName = je[1].GetString() ?? "";
                else if (odooProduct.brand_id is List<object> list && list.Count > 1)
                    brandName = list[1]?.ToString() ?? "";
                else if (odooProduct.brand_id is object[] arr && arr.Length > 1)
                    brandName = arr[1]?.ToString() ?? "";
                else if (odooProduct.brand_id is string s)
                    brandName = s;
            } catch {}
            
            // Mapeo Side y Brand (desde atributos de variante)
            string sideValue = "";
            if (attributeMap != null)
            {
                var attributeIds = new HashSet<long>();
                ExtractAttributeIds(odooProduct.product_template_attribute_value_ids, attributeIds);
                ExtractAttributeIds(odooProduct.product_template_variant_value_ids, attributeIds);

                foreach (var attrId in attributeIds)
                {
                    if (attributeMap.TryGetValue(attrId, out var info))
                    {
                        string attrName = info.AttributeName ?? "";
                        if (attrName.Contains("LADO", StringComparison.OrdinalIgnoreCase) || 
                            attrName.Contains("SIDE", StringComparison.OrdinalIgnoreCase) || 
                            attrName.Contains("POSICIÓN", StringComparison.OrdinalIgnoreCase))
                        {
                            sideValue = info.ValueName;
                        }
                        else if (string.IsNullOrEmpty(brandName) && 
                                (attrName.Contains("MARCA", StringComparison.OrdinalIgnoreCase) || 
                                 attrName.Contains("BRAND", StringComparison.OrdinalIgnoreCase)))
                        {
                            brandName = info.ValueName;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(brandName))
                _logger.LogInformation("[DEBUG-SYNC] Found BRAND for SKU {Sku}: {Brand}", skuCode, brandName);
            if (!string.IsNullOrEmpty(sideValue))
                _logger.LogInformation("[DEBUG-SYNC] Found SIDE for SKU {Sku}: {Side}", skuCode, sideValue);

            product.Brand = brandName;
            product.Sides = sideValue;
            product.Barcode = (odooProduct.Barcode == "0" || odooProduct.Barcode == "false" || string.IsNullOrEmpty(odooProduct.Barcode)) ? "" : odooProduct.Barcode;
            product.PhysicalAttributes = PhysicalAttributes.Create(odooProduct.Weight, 0, 0, 0);
            product.ImageSource = imageSource;
            product.HasImage = isValidImage(odooProduct.Image_128) || isValidImage(odooProduct.Image_1920);
            product.OdooId = odooProduct.Id;
            product.IsArchived = !odooProduct.Active;
            
            // Mapeo Packagings (Union of packaging_ids and bulk_ids)
            product.Packagings = new List<ProductPackaging>();
            var pIds = new HashSet<long>();
            ExtractAttributeIds(odooProduct.packaging_ids, pIds);
            ExtractAttributeIds(odooProduct.bulk_ids, pIds);

            if (packagingMap != null)
            {
                foreach (var pid in pIds)
                {
                    if (packagingMap.TryGetValue(pid, out var pkg))
                    {
                        product.Packagings.Add(new ProductPackaging
                        {
                            OdooId = pkg.Id,
                            Name = pkg.Name ?? "Bulto",
                            Qty = (decimal)pkg.NormalizedQty,
                            Weight = (decimal)pkg.NormalizedWeight,
                            Length = (decimal)pkg.NormalizedLength,
                            Width = (decimal)pkg.NormalizedWidth,
                            Height = (decimal)pkg.NormalizedHeight
                        });
                    }
                }
            }

            product.LastUpdated = DateTime.UtcNow; 

            _session.Store(product);
            // Redundant save removed: handled at batch level in SyncAllAsync
        }

        public async Task SyncSingleAsync(IOdooRpcClient odooClient, string sku)
        {
            var fields = new string[] { 
                "id", "display_name", "default_code", "active", "write_date", "categ_id",
                "brand_id", "weight", "barcode", "description", "image_128", "image_1920",
                "bulk_ids", "product_template_attribute_value_ids", "product_template_variant_value_ids"
            };

            // 1. Fetch Product
            var odooProducts = await odooClient.SearchAndReadAsync<OdooProductDto>("product.product", new object[][] { new object[] { "default_code", "ilike", sku } }, fields);
            if (odooProducts == null || odooProducts.Count == 0)
            {
                odooProducts = await odooClient.SearchAndReadAsync<OdooProductDto>("product.template", new object[][] { new object[] { "default_code", "ilike", sku } }, fields);
            }

            if (odooProducts == null || odooProducts.Count == 0)
            {
                _logger.LogWarning("[SINGLE-SYNC] Product {Sku} not found in Odoo.", sku);
                return;
            }

            var p = odooProducts[0];

            // 2. Fetch Attributes
            var attributeIds = new HashSet<long>();
            ExtractAttributeIds(p.product_template_attribute_value_ids, attributeIds);
            ExtractAttributeIds(p.product_template_variant_value_ids, attributeIds);
            
            Dictionary<long, (string AttributeName, string ValueName)>? attributeMap = null;
            if (attributeIds.Count > 0)
            {
                try {
                    var attrIds = attributeIds.Select(id => (object)id).ToArray();
                    var attrs = await odooClient.SearchAndReadAsync<OdooAttributeDto>("product.template.attribute.value", new object[][] { new object[] { "id", "in", attrIds } }, new[] { "id", "attribute_id", "name" });
                    if (attrs != null) {
                        attributeMap = new Dictionary<long, (string, string)>();
                        foreach (var a in attrs) {
                            string attrName = "";
                            if (a.Attribute_Id is System.Collections.IEnumerable en && !(a.Attribute_Id is string)) {
                                var l = new List<object>(); foreach (var i in en) l.Add(i);
                                if (l.Count > 1) attrName = l[1]?.ToString() ?? "";
                            }
                            attributeMap[a.Id] = (attrName, a.Name ?? "");
                        }
                    }
                } catch (Exception ex) { _logger.LogError(ex, "[SINGLE-SYNC] Error fetching attributes for {Sku}", sku); }
            }

            // 3. Fetch Packagings (Standard & Bulk)
            var packagingMap = new Dictionary<long, OdooPackagingDto>();
            var bulkIds = new HashSet<long>();
            ExtractAttributeIds(p.bulk_ids, bulkIds);

            if (bulkIds.Count > 0)
            {
                try {
                    var bIds = bulkIds.Select(id => (object)id).ToArray();
                    var bulks = await odooClient.SearchAndReadAsync<OdooPackagingDto>("stock.move.bulk", new object[][] { new object[] { "id", "in", bIds } }, new[] { "id", "name", "qty_bulk", "l_cm", "w_cm", "h_cm", "weight" });
                    if (bulks != null) {
                        foreach (var b in bulks) packagingMap[b.Id] = b;
                    }
                } catch (Exception ex) { _logger.LogError(ex, "[SINGLE-SYNC] Error fetching bulk packagings for {Sku}", sku); }
            }

            // Standard packagings (if any)
            var pIds = new HashSet<long>();
            ExtractAttributeIds(p.packaging_ids, pIds);
            if (pIds.Count > 0)
            {
                try {
                    var packIds = pIds.Select(id => (object)id).ToArray();
                    var packs = await odooClient.SearchAndReadAsync<OdooPackagingDto>("product.packaging", new object[][] { new object[] { "id", "in", packIds } }, new[] { "id", "name", "qty", "packaging_length", "packaging_width", "packaging_height", "max_weight" });
                    if (packs != null) {
                        foreach (var pkg in packs) packagingMap[pkg.Id] = pkg;
                    }
                } catch (Exception ex) { _logger.LogError(ex, "[SINGLE-SYNC] Error fetching standard packagings for {Sku}", sku); }
            }

            // 4. Sync
            await SyncProduct(p, attributeMap, packagingMap);
            await _session.SaveChangesAsync();
            _logger.LogInformation("[SINGLE-SYNC] Product {Sku} synced successfully with {PkgCount} packagings.", sku, packagingMap.Count);
        }
    }
}
