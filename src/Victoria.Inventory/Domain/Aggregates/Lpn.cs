using System;
using System.Collections.Generic;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Events;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Aggregates
{
    public sealed class Lpn
    {
        public string Id { get; private set; }
        public TenantId Tenant { get; private set; }
        public LpnCode Code { get; private set; }
        public Sku Sku { get; private set; }
        public int Quantity { get; private set; }
        public LpnStatus Status { get; private set; }
        public string? CurrentLocation { get; private set; }
        public string? SelectedOrderId { get; private set; }
        public string? ParentLpnId { get; private set; }
        
        private readonly List<IDomainEvent> _changes = new();
        public IReadOnlyCollection<IDomainEvent> Changes => _changes.AsReadOnly();

        private Lpn() { } // For pattern matching or internal use

        public static Lpn Create(string tenantId, string id, LpnCode code, Sku sku, int quantity, string userId, string stationId)
        {
            var lpn = new Lpn();
            var @event = new LpnCreated(tenantId, id, code.Value, sku.Value, quantity, DateTime.UtcNow, userId, stationId);
            lpn.Apply(@event);
            lpn._changes.Add(@event);
            return lpn;
        }

        public void Receive(string orderId, string userId, string stationId)
        {
            var @event = new LpnReceived(Tenant.Value, Id, orderId, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void Putaway(string targetLocation, string userId, string stationId)
        {
            var moveEvent = new LpnLocationChanged(Tenant.Value, Id, targetLocation, CurrentLocation ?? "RECEIPT", DateTime.UtcNow, userId, stationId);
            Apply(moveEvent);
            _changes.Add(moveEvent);

            var putawayEvent = new PutawayCompleted(Tenant.Value, Id, targetLocation, DateTime.UtcNow, userId, stationId);
            Apply(putawayEvent);
            _changes.Add(putawayEvent);
        }

        public void Allocate(string orderId, Sku sku, string userId, string stationId)
        {
            if (Status != LpnStatus.Putaway)
                throw new InvalidOperationException($"Only LPNs in Putaway status can be allocated. Current status: {Status}");

            if (Sku != sku)
                throw new ArgumentException($"SKU mismatch. Expected: {Sku}, Requested: {sku}");

            var @event = new LpnAllocated(Tenant.Value, Id, orderId, sku.Value, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void Pick(string userId, string stationId)
        {
            if (Status != LpnStatus.Allocated)
                throw new InvalidOperationException($"LPN must be Allocated before Picking. Current status: {Status}");

            var @event = new LpnPicked(Tenant.Value, Id, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void Ship(string userId)
        {
            if (Status != LpnStatus.Picked && Status != LpnStatus.Putaway) // Simplificación para el skeleton
                // En producción: Debe estar Picked y opcionalmente en un Container
                Status = LpnStatus.Dispatched;
            
            // Nota: Aquí se generaría un evento LpnDispatched si fuera necesario a nivel LPN
            // Para la fase 8 usaremos el evento de negocio DispatchConfirmed en el Service.
            Status = LpnStatus.Dispatched;
        }

        public void SetParent(string parentLpnId)
        {
            ParentLpnId = parentLpnId;
        }

        public void ReportCount(int currentQuantity, string userId, string stationId)
        {
            var @event = new InventoryCountCompleted(Tenant.Value, Id, Quantity, currentQuantity, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void AdjustQuantity(int newQuantity, string reason, string userId, string stationId)
        {
            var @event = new InventoryAdjusted(Tenant.Value, Id, Quantity, newQuantity, reason, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void Quarantine(string reason, string userId, string stationId)
        {
            var @event = new LpnQuarantined(Tenant.Value, Id, reason, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        private void Apply(IDomainEvent @event)
        {
            switch (@event)
            {
                case LpnCreated e:
                    Id = e.LpnId;
                    Tenant = TenantId.Create(e.TenantId);
                    Code = LpnCode.Create(e.LpnCode);
                    Sku = Sku.Create(e.Sku);
                    Quantity = e.Quantity;
                    Status = LpnStatus.Created;
                    break;
                case LpnReceived e:
                    Status = LpnStatus.Received;
                    break;
                case LpnLocationChanged e:
                    CurrentLocation = e.NewLocation;
                    break;
                case PutawayCompleted e:
                    Status = LpnStatus.Putaway;
                    break;
                case LpnAllocated e:
                    Status = LpnStatus.Allocated;
                    SelectedOrderId = e.OrderId;
                    break;
                case LpnPicked e:
                    Status = LpnStatus.Picked;
                    break;
                case PackingCompleted e:
                    Status = LpnStatus.Putaway; // Los contenedores maestros nacen ubicables o en staging
                    break;
                case InventoryCountCompleted e:
                    // El conteo por sí solo no cambia estado, solo audita
                    break;
                case InventoryAdjusted e:
                    Quantity = e.NewQuantity;
                    break;
                case LpnQuarantined e:
                    Status = LpnStatus.Quarantine;
                    break;
            }
        }

        public void ClearChanges() => _changes.Clear();
    }

    public enum LpnStatus
    {
        Created,
        Received,
        Putaway,
        Allocated,
        Picked,
        Dispatched,
        Shipped,
        Quarantine,
        Blocked
    }
}
