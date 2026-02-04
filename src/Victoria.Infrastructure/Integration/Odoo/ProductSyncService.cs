using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Victoria.Infrastructure.Integration.Odoo
{
    // DTO Sucio de Odoo
    public class OdooProductDto
    {
        public int OdooId { get; set; }
        public int CompanyId { get; set; } // 1=PERFECTPTY, 2=NATSUKI...
        public string Name { get; set; } = string.Empty;
        public string InternalReference { get; set; } = string.Empty; // SKU
        public double Weight { get; set; }
        public double Volume { get; set; }
    }

    public class ProductSyncService
    {
        private static readonly Dictionary<int, string> TenantMapping = new()
        {
            { 1, "PERFECTPTY" },
            { 2, "NATSUKI" },
            { 3, "PDM" },
            { 4, "FILTROS" }
        };

        public async Task SyncProduct(OdooProductDto odooProduct)
        {
            if (!TenantMapping.TryGetValue(odooProduct.CompanyId, out var tenantId))
                throw new ArgumentException($"Unknown Odoo CompanyId: {odooProduct.CompanyId}");

            // ACL LOGIC: Mapeo y Normalización
            string skuCode = odooProduct.InternalReference.ToUpper().Trim();

            Console.WriteLine($"[ACL] Syncing Product {skuCode} for Tenant {tenantId}");
            
            // Simulación de interacción con el repositorio de SKUs (Victoria.Inventory)
            // En una app real: _skuRepository.Upsert(new Sku(tenantId, skuCode, odooProduct.Name, odooProduct.Weight));
            
            await Task.CompletedTask;
        }
    }
}
