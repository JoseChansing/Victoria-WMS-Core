using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Npgsql;
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
    }

    public class ProductSyncService
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductSyncService> _logger;
        private static readonly Dictionary<int, string> TenantMapping = new()
        {
            { 1, "PERFECTPTY" },
            { 2, "NATSUKI" },
            { 3, "PDM" },
            { 4, "FILTROS" }
        };

        public ProductSyncService(ILogger<ProductSyncService> logger, IConfiguration config)
        {
            _logger = logger;
            _connectionString = config["POSTGRES_CONNECTION"] ?? "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";
        }

        public async Task SyncProduct(OdooProductDto odooProduct)
        {
            if (!TenantMapping.TryGetValue(odooProduct.Company_Id, out var tenantId))
                return;

            string skuCode = (odooProduct.Default_Code ?? "").ToUpper().Trim();
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

            _logger.LogInformation("[ProductSync] Persisting SKU '{Sku}' | Source: {Src} for {Tenant}", skuCode, imageSource, tenantId);
            
            var product = new Product
            {
                Id = $"{tenantId}-{skuCode}", // ID Compuesto
                Sku = skuCode,
                Name = odooProduct.Display_Name,
                TenantId = tenantId,
                Weight = odooProduct.Weight,
                ImageSource = imageSource,
                Thumbnail = thumbnail,
                OdooId = odooProduct.Id,
                LastUpdated = DateTime.UtcNow
            };

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var json = JsonSerializer.Serialize(product);
            var sql = @"
                INSERT INTO Products (Id, Sku, Name, TenantId, OdooId, Data)
                VALUES (@id, @sku, @name, @tenant, @odooId, @data::jsonb)
                ON CONFLICT (Id) DO UPDATE SET 
                    Data = EXCLUDED.Data,
                    Name = EXCLUDED.Name,
                    Sku = EXCLUDED.Sku;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", product.Id);
            cmd.Parameters.AddWithValue("sku", product.Sku);
            cmd.Parameters.AddWithValue("name", product.Name);
            cmd.Parameters.AddWithValue("tenant", product.TenantId);
            cmd.Parameters.AddWithValue("odooId", product.OdooId);
            cmd.Parameters.AddWithValue("data", json);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
