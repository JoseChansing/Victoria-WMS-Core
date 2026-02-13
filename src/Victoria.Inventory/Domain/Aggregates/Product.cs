using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
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
        public string Brand { get; set; } = string.Empty; // x_marca
        public string Sides { get; set; } = string.Empty; // x_lados
        public PhysicalAttributes PhysicalAttributes { get; set; } = PhysicalAttributes.Empty();
        public string ImageSource { get; set; } = "null"; // variant, thumbnail, null
        public string Thumbnail { get; set; } = string.Empty; // Base64
        public int OdooId { get; set; }
        public int OdooTemplateId { get; set; }
        public bool HasImage { get; set; }
        public bool IsArchived { get; set; }
        public List<ProductPackaging> Packagings { get; set; } = new();
        public bool HasPackaging => Packagings?.Any() ?? false;

        [JsonProperty]
        public decimal UnitWeight
        {
            get
            {
                try
                {
                    if (Packagings == null || !Packagings.Any())
                    {
                        var w = PhysicalAttributes?.Weight ?? 0;
                        return double.IsFinite(w) ? (decimal)w : 0;
                    }

                    var minPkg = Packagings.Where(p => p.Qty > 0).OrderBy(p => p.Qty).FirstOrDefault();
                    if (minPkg == null)
                    {
                        var w = PhysicalAttributes?.Weight ?? 0;
                        return double.IsFinite(w) ? (decimal)w : 0;
                    }

                    return minPkg.Weight / minPkg.Qty;
                }
                catch { return 0; }
            }
        }

        [JsonProperty]
        public decimal UnitVolume
        {
            get
            {
                try
                {
                    if (Packagings == null || !Packagings.Any())
                        return 0;

                    var minPkg = Packagings.Where(p => p.Qty > 0).OrderBy(p => p.Qty).FirstOrDefault();
                    if (minPkg == null) return 0;

                    var volume = minPkg.Length * minPkg.Width * minPkg.Height;
                    return minPkg.Qty > 0 ? volume / minPkg.Qty : 0;
                }
                catch { return 0; }
            }
        }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class ProductPackaging
    {
        public int OdooId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal Weight { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
    }
}
