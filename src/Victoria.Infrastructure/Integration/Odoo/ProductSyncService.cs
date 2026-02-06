using System;
using Marten;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Victoria.Inventory.Domain.Aggregates;

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
        public string? Image_1920 { get; set; } // image_1920 (Base64)
        public string? Image_Template { get; set; } // placeholder for template
        public string? Brand_Logo { get; set; }
        public string? Image_128 { get; set; } // thumbnail
        public string? Detailed_Type { get; set; } // product (storable), service, consu
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

        public async Task SyncProduct(OdooProductDto odooProduct)
        {

            string skuCode = (odooProduct.Default_Code ?? "").ToUpper().Trim();

            _logger.LogInformation("[FILTER-CHECK] Processing '{Name}' | Code: '{Code}' | Type: '{Type}'", 
                odooProduct.Display_Name, skuCode, odooProduct.Detailed_Type);

            // GUARD: Ignorar explícitamente basura conocida
            if (skuCode == "0" || odooProduct.Display_Name.Contains("Settle Invoice", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[FILTER-GUARD] Skipping system product '{Name}'", odooProduct.Display_Name);
                return;
            }

            if (string.IsNullOrEmpty(skuCode)) 
                skuCode = $"ODOO-{odooProduct.Id}"; // Fallback para que los productos aparezcan

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

            _logger.LogInformation("[ProductSync-Marten] Persisting SKU '{Sku}' | Source: {Src}", skuCode, imageSource);
            
            var product = new Product
            {
                Id = skuCode, // ID única natural en DB aislada
                Sku = skuCode,
                Name = odooProduct.Display_Name,
                Weight = odooProduct.Weight,
                ImageSource = imageSource,
                Thumbnail = thumbnail,
                OdooId = odooProduct.Id,
                LastUpdated = DateTime.UtcNow
            };

            _session.Store(product);
            await _session.SaveChangesAsync();
        }
    }
}
