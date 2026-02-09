using System;

namespace Victoria.Inventory.Domain.Aggregates
{
    public class Product
    {
        public string Id { get; set; } = string.Empty; // SKU or Odoo ID
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public double Weight { get; set; }
        public string ImageSource { get; set; } = "null"; // variant, thumbnail, null
        public string Thumbnail { get; set; } = string.Empty; // Base64
        public int OdooId { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
