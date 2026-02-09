using System;
using System.Collections.Generic;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Events;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Aggregates
{
    public enum LocationProfile
    {
        Reserve,
        Picking
    }

    public sealed class Location
    {
        public string Id { get; set; } = string.Empty;
        public LocationCode Code { get; set; }
        public LocationStatus Status { get; set; }
        public LocationProfile Profile { get; set; }
        public bool IsPickable { get; set; }
        public LpnCode? AssignedLpn { get; set; }

        // Metadata additions
        public int PickingSequence { get; set; }
        public double MaxWeight { get; set; }
        public double MaxVolume { get; set; }
        public string Barcode { get; set; } = string.Empty;

        private readonly List<IDomainEvent> _changes = new();
        public IReadOnlyCollection<IDomainEvent> Changes => _changes.AsReadOnly();

        public Location() { }

        public static Location Create(LocationCode code, LocationProfile profile, bool isPickable)
        {
            var loc = new Location();
            var @event = new LocationCreated(code.Value, code.Zone, profile, isPickable, DateTime.UtcNow, "SYS", "SYS");
            loc.Apply(@event);
            loc._changes.Add(@event);
            return loc;
        }

        public void AssignLpn(LpnCode lpnCode, string userId, string stationId)
        {
            if (Status != LocationStatus.Empty)
                throw new InvalidOperationException($"Location {Code} is not empty. Current status: {Status}");

            var @event = new LocationAssigned(Code.Value, lpnCode.Value, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        public void UpdateMetadata(int gatheringSequence, double maxWeight, double maxVolume, string barcode, string userId, string stationId)
        {
            var @event = new LocationMetadataUpdated(Code.Value, gatheringSequence, maxWeight, maxVolume, barcode, DateTime.UtcNow, userId, stationId);
            Apply(@event);
            _changes.Add(@event);
        }

        private void Apply(IDomainEvent @event)
        {
            switch (@event)
            {
                case LocationCreated e:
                    Id = e.LocationCode;
                    Code = LocationCode.Create(e.LocationCode);
                    Status = LocationStatus.Empty;
                    Profile = e.Profile;
                    IsPickable = e.IsPickable;
                    break;
                case LocationAssigned e:
                    AssignedLpn = LpnCode.Create(e.LpnCode);
                    Status = LocationStatus.Occupied;
                    break;
                case LocationMetadataUpdated e:
                    PickingSequence = e.PickingSequence;
                    MaxWeight = e.MaxWeight;
                    MaxVolume = e.MaxVolume;
                    Barcode = e.Barcode;
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
