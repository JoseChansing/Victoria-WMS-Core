using System;

namespace Victoria.Inventory.Domain.Entities
{
    public class SkuPackingRule
    {
        public string TenantId { get; }
        public string Sku { get; }
        public int MinOrderQty { get; }
        public int OrderMultiple { get; }

        public SkuPackingRule(string tenantId, string sku, int minOrderQty, int orderMultiple)
        {
            TenantId = tenantId;
            Sku = sku;
            MinOrderQty = minOrderQty;
            OrderMultiple = orderMultiple;
            
            if (orderMultiple <= 0) throw new ArgumentException("OrderMultiple must be greater than zero.");
        }

        public int CalculateValidQuantity(int requestedQty)
        {
            if (requestedQty < MinOrderQty) return MinOrderQty;
            
            int remainder = requestedQty % OrderMultiple;
            if (remainder == 0) return requestedQty;
            
            return requestedQty + (OrderMultiple - remainder);
        }

        public bool IsValidQuantity(int requestedQty)
        {
            return requestedQty >= MinOrderQty && (requestedQty % OrderMultiple) == 0;
        }
    }
}
