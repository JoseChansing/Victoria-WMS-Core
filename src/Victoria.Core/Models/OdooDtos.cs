using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Victoria.Core.Models
{
    public class OdooProductDto
    {
        public int Id { get; set; }
        public int Company_Id { get; set; }
        public string? Display_Name { get; set; }
        public string? Default_Code { get; set; }
        public double Weight { get; set; }
        public string? Barcode { get; set; }
        public string? Description { get; set; }
        public string? Image_1920 { get; set; }
        public string? Image_128 { get; set; }
        public string? Type { get; set; } 
        public bool Active { get; set; }
        public object? Categ_Id { get; set; }
        public string? Write_Date { get; set; }
        public object? brand_id { get; set; }
        public string? x_lados { get; set; } 
        
        [JsonPropertyName("product_template_attribute_value_ids")]
        public object? product_template_attribute_value_ids { get; set; }

        [JsonPropertyName("product_template_variant_value_ids")]
        public object? product_template_variant_value_ids { get; set; }

        public object? packaging_ids { get; set; }
        public object? bulk_ids { get; set; }
    }

    public class OdooPackagingDto
    {
        public int Id { get; set; }
        public string? Name { get; set; } = string.Empty;

        [JsonPropertyName("qty")]
        public double Qty { get; set; }

        [JsonPropertyName("qty_bulk")]
        public double QtyBulk { get; set; }

        [JsonPropertyName("packaging_length")]
        public double PackagingLength { get; set; }
        
        [JsonPropertyName("l_cm")]
        public double LCm { get; set; }

        [JsonPropertyName("packaging_width")]
        public double PackagingWidth { get; set; }

        [JsonPropertyName("w_cm")]
        public double WCm { get; set; }

        [JsonPropertyName("packaging_height")]
        public double PackagingHeight { get; set; }

        [JsonPropertyName("h_cm")]
        public double HCm { get; set; }

        [JsonPropertyName("max_weight")]
        public double MaxWeight { get; set; }

        [JsonPropertyName("weight")]
        public double Weight { get; set; }

        // Logic Helpers
        [JsonIgnore]
        public double NormalizedQty => Qty > 0 ? Qty : QtyBulk;
        [JsonIgnore]
        public double NormalizedLength => LCm > 0 ? LCm : PackagingLength;
        [JsonIgnore]
        public double NormalizedWidth => WCm > 0 ? WCm : PackagingWidth;
        [JsonIgnore]
        public double NormalizedHeight => HCm > 0 ? HCm : PackagingHeight;
        [JsonIgnore]
        public double NormalizedWeight => Weight > 0 ? Weight : MaxWeight;
    }

    public class OdooAttributeDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("attribute_id")]
        public object? Attribute_Id { get; set; }
    }

    public class OdooOrderLineDto
    {
        public long Id { get; set; }
        public object? Product_Id { get; set; }
        public double Product_Uom_Qty { get; set; }
    }

    public class OdooOrderDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Company_Id { get; set; }
        public string Picking_Type_Code { get; set; } = string.Empty;
        public string Write_Date { get; set; } = string.Empty;
        public List<OdooOrderLineDto> Lines { get; set; } = new();
    }
}
