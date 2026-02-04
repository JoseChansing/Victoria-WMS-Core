using System;
using System.Collections.Generic;
using System.Linq;
using Victoria.Inventory.Domain.Entities;

namespace Victoria.Inventory.Domain.Aggregates
{
    public class OutboundOrder
    {
        public string OrderId { get; }
        public string TenantId { get; }
        private readonly List<SalesOrderLine> _lines = new List<SalesOrderLine>();
        public IReadOnlyCollection<SalesOrderLine> Lines => _lines.AsReadOnly();

        public OutboundOrder(string tenantId, string orderId)
        {
            TenantId = tenantId;
            OrderId = orderId;
        }

        public void AddLine(string lineId, string sku, int qty)
        {
            _lines.Add(new SalesOrderLine(lineId, sku, qty));
        }

        public bool CanShip()
        {
            return _lines.All(l => l.Status != OrderLineStatus.PendingApproval);
        }

        public void RequestOverage(string lineId, int requestedQty)
        {
            var line = _lines.FirstOrDefault(l => l.LineId == lineId);
            if (line == null) throw new InvalidOperationException("Line not found");
            
            line.RequestOverage(requestedQty);
        }

        public void ApproveOverage(string lineId, int approvedQty)
        {
            var line = _lines.FirstOrDefault(l => l.LineId == lineId);
            if (line == null) throw new InvalidOperationException("Line not found");

            line.ApproveOverage(approvedQty);
        }
    }
}
