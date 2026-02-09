using System;
using Victoria.Inventory.Domain.ValueObjects;

namespace Victoria.Inventory.Domain.Aggregates
{
    public class Product
    {
        public string Id { get; set; } = string.Empty; // SKU or Odoo ID
        public string Sku { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public PhysicalAttributes PhysicalAttributes { get; set; } = PhysicalAttributes.Empty();
        public string ImageSource { get; set; } = "null"; // variant, thumbnail, null
        public string Thumbnail { get; set; } = string.Empty; // Base64
        public int OdooId { get; set; }
        public bool IsArchived { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
