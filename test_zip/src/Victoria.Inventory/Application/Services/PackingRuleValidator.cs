using System;
using System.Collections.Generic;
using Victoria.Inventory.Domain.Entities;

namespace Victoria.Inventory.Application.Services
{
    public interface IPackingRuleValidator
    {
        (bool IsValid, int SuggestedQty, string Message) Validate(string tenantId, string sku, int requestedQty);
    }

    public class PackingRuleValidator : IPackingRuleValidator
    {
        // En una implementación real, esto consultaría un repositorio de reglas.
        private readonly List<SkuPackingRule> _rules = new List<SkuPackingRule>();

        public PackingRuleValidator()
        {
            // Dummy rule for the demo: SKU-001 must be in multiples of 2 (pairs).
            _rules.Add(new SkuPackingRule("PERFECTPTY", "SKU-001", 1, 2));
        }

        public (bool IsValid, int SuggestedQty, string Message) Validate(string tenantId, string sku, int requestedQty)
        {
            var rule = _rules.Find(r => r.TenantId == tenantId && r.Sku == sku);
            
            if (rule == null) return (true, requestedQty, "No rules defined for this SKU.");

            if (rule.IsValidQuantity(requestedQty))
            {
                return (true, requestedQty, "Quantity matches packing rules.");
            }

            int suggested = rule.CalculateValidQuantity(requestedQty);
            return (false, suggested, $"Order Quantity Adjustment Required: {requestedQty} is invalid. Suggested: {suggested} (Multiple of {rule.OrderMultiple})");
        }
    }
}
