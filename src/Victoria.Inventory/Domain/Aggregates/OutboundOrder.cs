using System;
using System.Collections.Generic;
using System.Linq;
using Victoria.Inventory.Domain.Entities;

namespace Victoria.Inventory.Domain.Aggregates
{
    public class OutboundOrder
    {
        public string Id { get; set; } // Marten ID = Odoo Name (e.g. WH/OUT/0001)
        public string OrderId { get; private set; } // External ID / Name from Odoo
        
        // Odoo Properties
        public int OdooId { get; private set; } // Odoo ID (stock.picking id)
        public string PartnerId { get; private set; }
        public string Priority { get; private set; }
        public string ScheduledDate { get; private set; }
        
        public string? ExtensionWaveId { get; private set; } // Assigned Wave
        
        private readonly List<OutboundLine> _lines = new List<OutboundLine>();
        public IReadOnlyCollection<OutboundLine> Lines => _lines.AsReadOnly();

        public OutboundOrder(string orderId, int odooId, string partnerId, string priority, string scheduledDate)
        {
            Id = orderId; // Use Name as ID
            OrderId = orderId;
            OdooId = odooId;
            PartnerId = partnerId;
            Priority = priority;
            ScheduledDate = scheduledDate;
        }

        public void AddLine(OutboundLine line)
        {
            _lines.Add(line);
        }

        public void AssignWave(string waveId)
        {
            ExtensionWaveId = waveId;
        }
    }
}
