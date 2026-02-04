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
        public LpnCode Code { get; private set; }
        public Sku Sku { get; private set; }
        public int Quantity { get; private set; }
        public LpnStatus Status { get; private set; }
        
        private readonly List<IDomainEvent> _changes = new();
        public IReadOnlyCollection<IDomainEvent> Changes => _changes.AsReadOnly();

        private Lpn() { } // For pattern matching or internal use

        public static Lpn Create(string id, LpnCode code, Sku sku, int quantity, string userId, string stationId)
        {
            var lpn = new Lpn();
            var @event = new LpnCreated(id, code.Value, sku.Value, quantity, DateTime.UtcNow, userId, stationId);
            lpn.Apply(@event);
            lpn._changes.Add(@event);
            return lpn;
        }

        public void Receive(string orderId, string userId, string stationId)
        {
            if (Status == LpnStatus.Shipped)
                throw new InvalidOperationException("Cannot receive an LPN that has already been shipped.");
            
            if (Status == LpnStatus.Received)
                throw new InvalidOperationException("LPN is already received.");

            var @event = new ReceiptCompleted(Id, orderId, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        private void Apply(IDomainEvent @event)
        {
            switch (@event)
            {
                case LpnCreated e:
                    Id = e.LpnId;
                    Code = LpnCode.Create(e.LpnCode);
                    Sku = Sku.Create(e.Sku);
                    Quantity = e.Quantity;
                    Status = LpnStatus.Created;
                    break;
                case ReceiptCompleted e:
                    Status = LpnStatus.Received;
                    break;
            }
        }

        public void ClearChanges() => _changes.Clear();
    }

    public enum LpnStatus
    {
        Created,
        Received,
        Shipped
    }
}
