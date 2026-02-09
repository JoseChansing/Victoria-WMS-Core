using System;
using System.Collections.Generic;
using System.Linq;
using Marten.Events.Projections;
using Victoria.Inventory.Domain.Events;

namespace Victoria.Infrastructure.Projections
{
    public class InventoryItemView
    {
        public string Id { get; set; } // Sku
        public string Sku { get; set; }
        public string Description { get; set; } // Added
        public int TotalQuantity { get; set; }
        public Dictionary<string, int> LpnQuantities { get; set; } = new(); // Idempotency: LpnId -> Quantity
        public string PrimaryLocation { get; set; } // Added
        public DateTime LastUpdated { get; set; }
    }

    public class InventoryByItemProjection : MultiStreamProjection<InventoryItemView, string>
    {
        public InventoryByItemProjection()
        {
            Identity<InventoryImportedFromOdoo>(e => e.Sku);
        }

        public void Apply(InventoryImportedFromOdoo e, InventoryItemView view)
        {
            view.Sku = e.Sku;
            view.Id = e.Sku;
            view.Description = e.Description;
            view.LpnQuantities[e.LpnId] = e.Quantity;
            view.TotalQuantity = view.LpnQuantities.Values.Sum();
            view.LastUpdated = e.ImportDate;
            view.PrimaryLocation = e.TargetLocation; // Simplified for now
        }
    }

    public class LocationInventoryView
    {
        public string Id { get; set; } // LocationId
        public string LocationType { get; set; } // Added
        public List<LpnDetailSummary> Lpns { get; set; } = new();
        public int TotalItems { get; set; }
        public Dictionary<string, int> LpnQuantities { get; set; } = new();
    }

    public class LpnDetailSummary
    {
        public string LpnId { get; set; }
        public string Sku { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public int AllocatedQuantity { get; set; }
        public int Status { get; set; }
    }

    public class InventoryByLocationProjection : MultiStreamProjection<LocationInventoryView, string>
    {
        public InventoryByLocationProjection()
        {
            Identity<InventoryImportedFromOdoo>(e => e.TargetLocation);
            Identity<LpnLocationChanged>(e => e.NewLocation);
        }

        public void Apply(InventoryImportedFromOdoo e, LocationInventoryView view)
        {
            view.Id = e.TargetLocation;
            view.LpnQuantities[e.LpnId] = e.Quantity;
            view.TotalItems = view.LpnQuantities.Values.Sum();
            
            var existingLpn = view.Lpns.FirstOrDefault(l => l.LpnId == e.LpnId);
            if (existingLpn != null)
            {
                existingLpn.Quantity = e.Quantity;
            }
            else
            {
                view.Lpns.Add(new LpnDetailSummary 
                { 
                    LpnId = e.LpnId, 
                    Sku = e.Sku, 
                    Description = e.Description,
                    Quantity = e.Quantity,
                    Status = 2 // Ubicado
                });
            }
        }

        public void Apply(LpnLocationChanged e, LocationInventoryView view)
        {
             // Phase 3 implementation
        }
    }
}
