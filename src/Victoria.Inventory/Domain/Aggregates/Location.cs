using System;
using System.Collections.Generic;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Events;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Aggregates
{
    public sealed class Location
    {
        public LocationCode Code { get; private set; }
        public TenantId Tenant { get; private set; }
        public LocationStatus Status { get; private set; }
        public LpnCode? AssignedLpn { get; private set; }

        private readonly List<IDomainEvent> _changes = new();
        public IReadOnlyCollection<IDomainEvent> Changes => _changes.AsReadOnly();

        private Location() { }

        public static Location Create(string tenantId, LocationCode code)
        {
            var loc = new Location();
            var @event = new LocationCreated(tenantId, code.Value, code.Zone, DateTime.UtcNow, "SYS", "SYS");
            loc.Apply(@event);
            loc._changes.Add(@event);
            return loc;
        }

        public void AssignLpn(LpnCode lpnCode, string userId, string stationId)
        {
            if (Status != LocationStatus.Empty)
                throw new InvalidOperationException($"Location {Code} is not empty. Current status: {Status}");

            var @event = new LocationAssigned(Tenant.Value, Code.Value, lpnCode.Value, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        private void Apply(IDomainEvent @event)
        {
            switch (@event)
            {
                case LocationCreated e:
                    Code = LocationCode.Create(e.LocationCode);
                    Status = LocationStatus.Empty;
                    Tenant = TenantId.Create(e.TenantId);
                    break;
                case LocationAssigned e:
                    AssignedLpn = LpnCode.Create(e.LpnCode);
                    Status = LocationStatus.Occupied;
                    break;
            }
        }

        public void ClearChanges() => _changes.Clear();
    }

    public enum LocationStatus
    {
        Empty,
        Occupied,
        Blocked
    }
}
