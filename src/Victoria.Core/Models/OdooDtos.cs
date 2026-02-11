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
