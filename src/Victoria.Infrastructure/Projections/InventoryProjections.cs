using System;
using System.Collections.Generic;
using System.Linq;
using Marten.Events.Projections;
using Marten.Events.Aggregation;
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
            Identity<LpnCreated>(e => e.Sku);
            Identity<LpnAllocated>(e => e.Sku);
        }

        public void Apply(InventoryImportedFromOdoo e, InventoryItemView view)
        {
            view.Sku = e.Sku;
            view.Id = e.Sku;
            view.Description = e.Description;
            view.LpnQuantities[e.LpnId] = e.Quantity;
            view.TotalQuantity = view.LpnQuantities.Values.Sum();
            view.LastUpdated = e.OccurredOn;
            view.PrimaryLocation = e.TargetLocation; 
        }

        public void Apply(LpnCreated e, InventoryItemView view)
        {
            view.Sku = e.Sku;
            view.Id = e.Sku;
            view.LpnQuantities[e.LpnId] = e.Quantity;
            view.TotalQuantity = view.LpnQuantities.Values.Sum();
            view.LastUpdated = e.OccurredOn;
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
            Identity<LpnCreated>(e => "DOCK-UNITS"); // Default
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
            view.Id = e.NewLocation;
        }
    }

    public class LpnDetailView
    {
        public string Id { get; set; }
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }

    public class LpnDetailProjection : SingleStreamProjection<LpnDetailView>
    {
        public void Apply(InventoryImportedFromOdoo e, LpnDetailView view)
        {
            view.Id = e.LpnId;
            view.Sku = e.Sku;
            view.Quantity += e.Quantity;
            view.Location = e.TargetLocation;
            view.Status = "Putaway";
            view.LastUpdated = e.OccurredOn;
        }

        public void Apply(LpnCreated e, LpnDetailView view)
        {
            view.Id = e.LpnId;
            view.Sku = e.Sku;
            view.Quantity = e.Quantity;
            view.Status = "Created";
            view.LastUpdated = e.OccurredOn;
        }

        public void Apply(LpnLocationChanged e, LpnDetailView view)
        {
            view.Location = e.NewLocation;
            view.LastUpdated = e.OccurredOn;
        }

        public void Apply(InventoryAdjusted e, LpnDetailView view)
        {
            view.Quantity = e.NewQuantity;
            view.LastUpdated = e.OccurredOn;
        }
    }

    public class LpnHistoryEntry
    {
        public string EventType { get; set; }
        public string Description { get; set; }
        public string User { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public class LpnHistoryView
    {
        public string Id { get; set; }
        public List<LpnHistoryEntry> Entries { get; set; } = new();
    }

    public class LpnHistoryProjection : SingleStreamProjection<LpnHistoryView>
    {
        public void Apply(InventoryImportedFromOdoo e, LpnHistoryView view)
        {
            view.Entries.Add(new LpnHistoryEntry 
            { 
                EventType = "IMPORT", 
                Description = $"Imported from Odoo Quant {e.OdooQuantId} to {e.TargetLocation}", 
                User = e.User, 
                Timestamp = e.OccurredOn 
            });
        }

        public void Apply(LpnCreated e, LpnHistoryView view)
        {
            view.Entries.Add(new LpnHistoryEntry 
            { 
                EventType = "CREATED", 
                Description = $"LPN Created with {e.Quantity} units of {e.Sku}", 
                User = e.CreatedBy, 
                Timestamp = e.OccurredOn 
            });
        }

        public void Apply(LpnLocationChanged e, LpnHistoryView view)
        {
            view.Entries.Add(new LpnHistoryEntry 
            { 
                EventType = "MOVE", 
                Description = $"Moved from {e.OldLocation} to {e.NewLocation}", 
                User = e.CreatedBy, 
                Timestamp = e.OccurredOn 
            });
        }
    }
}
