using System;

namespace Victoria.Inventory.Domain.Entities
{
    public enum OrderLineStatus
    {
        Open,
        PendingApproval,
        Approved,
        Rejected,
        Completed
    }

    public class SalesOrderLine
    {
        public string LineId { get; }
        public string Sku { get; }
        public int OrderedQuantity { get; private set; }
        public OrderLineStatus Status { get; private set; }

        public SalesOrderLine(string lineId, string sku, int orderedQuantity)
        {
            LineId = lineId;
            Sku = sku;
            OrderedQuantity = orderedQuantity;
            Status = OrderLineStatus.Open;
        }

        public void RequestOverage(int requestedQty)
        {
            Status = OrderLineStatus.PendingApproval;
        }

        public void ApproveOverage(int approvedQty)
        {
            OrderedQuantity = approvedQty;
            Status = OrderLineStatus.Approved;
        }

        public void RejectOverage()
        {
            Status = OrderLineStatus.Rejected;
        }
    }
}
