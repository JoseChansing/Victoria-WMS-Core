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
        public LocationStatus Status { get; private set; }
        public string? CurrentLpnCode { get; private set; }

        private readonly List<IDomainEvent> _changes = new();
        public IReadOnlyCollection<IDomainEvent> Changes => _changes.AsReadOnly();

        private Location() { }

        public static Location Create(LocationCode code, string userId, string stationId)
        {
            var location = new Location();
            var @event = new LocationCreated(code.Value, code.Zone, DateTime.UtcNow, userId, stationId);
            location.Apply(@event);
            location._changes.Add(@event);
            return location;
        }

        public void AssignLpn(LpnCode lpnCode, string userId, string stationId)
        {
            if (Status != LocationStatus.Empty)
                throw new InvalidOperationException($"Location {Code} is not empty. Current status: {Status}");

            var @event = new LpnAssignedToLocation(Code.Value, lpnCode.Value, DateTime.UtcNow, userId, stationId);
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
                    break;
                case LpnAssignedToLocation e:
                    CurrentLpnCode = e.LpnCode;
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
