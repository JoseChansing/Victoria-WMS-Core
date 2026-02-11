using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Events;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Aggregates
{
    public enum LpnType
    {
        Pallet,
        Loose
    }

    public sealed class Lpn
    {
        [JsonProperty] public string Id { get; private set; }
        [JsonProperty] public LpnCode Code { get; private set; }
        [JsonProperty] public Sku Sku { get; private set; }
        [JsonProperty] public LpnType Type { get; private set; }
        [JsonProperty] public int Quantity { get; private set; }
        [JsonProperty] public int AllocatedQuantity { get; private set; } // For partial reservations
        [JsonProperty] public PhysicalAttributes PhysicalAttributes { get; private set; } = PhysicalAttributes.Empty();
        [JsonProperty] public LpnStatus Status { get; private set; }
        [JsonProperty] public string? CurrentLocationId { get; private set; }
        [JsonProperty] public string? SelectedOrderId { get; private set; }
        [JsonProperty] public string? TargetOutboundOrder { get; private set; }
        [JsonProperty] public string? ParentLpnId { get; private set; }
        [JsonProperty] public string Brand { get; private set; } = "";
        [JsonProperty] public string Sides { get; private set; } = "";
        [JsonProperty] public string ProductBarcode { get; private set; } = "";
        [JsonProperty] public DateTime CreatedAt { get; private set; }
        
        private readonly List<IDomainEvent> _changes = new();
        public IReadOnlyCollection<IDomainEvent> Changes => _changes.AsReadOnly();

        [JsonConstructor]
        private Lpn(string id, LpnCode code, Sku sku, LpnType type, int quantity, PhysicalAttributes physicalAttributes, LpnStatus status, string? currentLocationId, string? selectedOrderId, string? targetOutboundOrder, string? parentLpnId, DateTime createdAt, string brand, string sides, string productBarcode)
        {
            Id = id;
            Code = code;
            Sku = sku;
            Type = type;
            Quantity = quantity;
            PhysicalAttributes = physicalAttributes;
            Status = status;
            CurrentLocationId = currentLocationId;
            SelectedOrderId = selectedOrderId;
            TargetOutboundOrder = targetOutboundOrder;
            ParentLpnId = parentLpnId;
            CreatedAt = createdAt;
            Brand = brand;
            Sides = sides;
            ProductBarcode = productBarcode;
        }

        private Lpn() { } // Marten fallback

        public static Lpn Provision(string id, LpnCode code, Sku sku, LpnType type, int quantity, PhysicalAttributes physicalAttributes, string userId, string stationId, string brand = "", string sides = "", string productBarcode = "")
        {
            if (sku == null) throw new ArgumentException("Un LPN no puede existir sin un SKU asociado.");

            var lpn = new Lpn();
            var @event = new LpnCreated(id, code.Value, sku.Value, type, quantity, physicalAttributes, DateTime.UtcNow, userId, stationId, brand, sides, productBarcode);
            lpn.Apply(@event);
            lpn._changes.Add(@event);
            return lpn;
        }

        public void Receive(string orderId, string userId, string stationId)
        {
            var @event = new LpnReceived(Id, orderId, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void Putaway(string targetLocation, string userId, string stationId)
        {
            var moveEvent = new LpnLocationChanged(Id, targetLocation, CurrentLocationId ?? "RECEIPT", DateTime.UtcNow, userId, stationId);
            Apply(moveEvent);
            _changes.Add(moveEvent);

            var putawayEvent = new PutawayCompleted(Id, targetLocation, DateTime.UtcNow, userId, stationId);
            Apply(putawayEvent);
            _changes.Add(putawayEvent);
        }

        public void Allocate(string orderId, Sku sku, string userId, string stationId)
        {
            if (Status != LpnStatus.Putaway)
                throw new InvalidOperationException($"Only LPNs in Putaway status can be allocated. Current status: {Status}");

            if (Sku != sku)
                throw new ArgumentException($"SKU mismatch. Expected: {Sku}, Requested: {sku}");

            var @event = new LpnAllocated(Id, orderId, sku.Value, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void ReserveQuantity(int qty)
        {
            if (AllocatedQuantity + qty > Quantity)
                throw new InvalidOperationException($"Cannot reserve {qty}. Available: {Quantity - AllocatedQuantity}");
            
            AllocatedQuantity += qty;
        }

        public void ReleaseReservation(int qty)
        {
            AllocatedQuantity -= qty;
            if (AllocatedQuantity < 0) AllocatedQuantity = 0;
        }

        public void Pick(string userId, string stationId)
        {
            if (Status != LpnStatus.Allocated)
                throw new InvalidOperationException($"LPN must be Allocated before Picking. Current status: {Status}");

            var @event = new LpnPicked(Id, DateTime.UtcNow, userId, stationId);
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
            var @event = new InventoryCountCompleted(Id, Quantity, currentQuantity, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void AdjustQuantity(int newQuantity, string reason, string userId, string stationId)
        {
            var @event = new InventoryAdjusted(Id, Quantity, newQuantity, reason, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void AddQuantity(int quantity, string userId, string stationId)
        {
            if (Type != LpnType.Loose)
                throw new InvalidOperationException("Solo se puede agregar cantidad a LPNs de tipo Loose. Los Pallets son unidades cerradas.");

            var newQuantity = Quantity + quantity;
            var @event = new InventoryAdjusted(Id, Quantity, newQuantity, "CONSOLIDATION_RECEIPT", DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void Quarantine(string reason, string userId, string stationId)
        {
            var @event = new LpnQuarantined(Id, reason, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void Apply(LpnCreated e)
        {
            Id = e.LpnId;
            Code = LpnCode.Create(e.LpnCode);
            Sku = Sku.Create(e.Sku);
            Type = e.Type;
            Quantity = e.Quantity;
            PhysicalAttributes = e.PhysicalAttributes;
            Status = LpnStatus.Created;
            CreatedAt = e.OccurredOn;
            Brand = e.Brand;
            Sides = e.Sides;
            ProductBarcode = e.ProductBarcode;
        }

        public void Apply(LpnReceived e)
        {
            Status = LpnStatus.Received;
            SelectedOrderId = e.OrderId;
        }

        public void SetTargetOrder(string targetOrder)
        {
            TargetOutboundOrder = targetOrder;
        }

        public void Apply(LpnLocationChanged e)
        {
            CurrentLocationId = e.NewLocation;
        }

        public void Apply(PutawayCompleted e)
        {
            Status = LpnStatus.Putaway;
        }

        public void Apply(LpnAllocated e)
        {
            Status = LpnStatus.Allocated;
            SelectedOrderId = e.OrderId;
        }

        public void Apply(LpnPicked e)
        {
            Status = LpnStatus.Picked;
        }

        public void Apply(PackingCompleted e)
        {
            Status = LpnStatus.Putaway; // Los contenedores maestros nacen ubicables o en staging
        }

        public void Apply(InventoryCountCompleted e)
        {
            // El conteo por sí solo no cambia estado, solo audita
        }

        public void Apply(InventoryAdjusted e)
        {
            Quantity = e.NewQuantity;
        }

        public void Apply(LpnQuarantined e)
        {
            Status = LpnStatus.Quarantine;
        }

        public void Apply(InventoryImportedFromOdoo e)
        {
            if (Status == LpnStatus.Created || string.IsNullOrEmpty(Id)) 
            {
                Id = e.LpnId;
                Code = LpnCode.Create(e.LpnId);
                Sku = Sku.Create(e.Sku);
                Type = LpnType.Loose;
                PhysicalAttributes = PhysicalAttributes.Empty();
                Status = LpnStatus.Putaway;
                CurrentLocationId = e.TargetLocation;
                CreatedAt = e.OccurredOn;
                Quantity = 0; // Initialize for addition
            }
            Quantity += e.Quantity; 
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
