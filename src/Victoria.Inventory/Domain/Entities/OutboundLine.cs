using System;

namespace Victoria.Inventory.Domain.Entities
{
    public class OutboundLine
    {
        public string LineId { get; private set; }
        // Odoo Properties
        public int OdooId { get; private set; } // Odoo Move ID
        public int ProductId { get; private set; }
        public string ProductName { get; private set; }
        public double ProductUomQty { get; private set; }
        public string ProductUomName { get; private set; }
        
        // Internal tracking
        public double PickedQty { get; private set; }
        
        public OutboundLine(string lineId, int odooId, int productId, string productName, double productUomQty, string productUomName)
        {
            LineId = lineId;
            OdooId = odooId;
            ProductId = productId;
            ProductName = productName;
            ProductUomQty = productUomQty;
            ProductUomName = productUomName;
            PickedQty = 0;
        }

        public void RegisterPick(double qty)
        {
            PickedQty += qty;
        }
    }
}
