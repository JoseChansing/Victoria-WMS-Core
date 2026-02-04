using System;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;

namespace Victoria.Inventory.Application.Commands
{
    public class ReceiveLpnCommand
    {
        public string LpnId { get; set; }
        public string OrderId { get; set; }
        public string UserId { get; set; }
        public string StationId { get; set; }
    }

    public class ReceiveLpnHandler
    {
        private readonly Victoria.Core.Infrastructure.IEventStore _eventStore;
        private readonly Victoria.Core.Infrastructure.ILockService _lockService;

        public ReceiveLpnHandler(
            Victoria.Core.Infrastructure.IEventStore eventStore,
            Victoria.Core.Infrastructure.ILockService lockService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
        }

        public async Task Handle(ReceiveLpnCommand command)
        {
            var lockKey = $"LOCK:LPN:{command.LpnId}";
            if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30)))
            {
                throw new InvalidOperationException($"Could not acquire lock for LPN {command.LpnId}");
            }

            try
            {
                // In real app: Load aggregate from event store
                // var events = await _eventStore.GetEventsAsync(command.LpnId);
                // var lpn = Lpn.Load(events);
                
                // For Walking Skeleton, we simulate a fresh aggregate or simple load
                var lpn = Lpn.Create(command.LpnId, LpnCode.Create("LPN1234567890"), Sku.Create("SKU-ABC-001"), 10, command.UserId, command.StationId);
                lpn.ClearChanges(); // Clear creation events if we assume it exists, or keep them if new. 
                // Let's assume for this flow we are receiving an EXISTING LPN (created previously)
                // But since DB is empty, effectively we are operating on a new object. 
                
                // Execute Domain Logic
                lpn.Receive(command.OrderId, command.UserId, command.StationId);

                // Persist Events
                await _eventStore.AppendEventsAsync(command.LpnId, -1, lpn.Changes);
            }
            finally
            {
                await _lockService.ReleaseLockAsync(lockKey);
            }
        }
    }
}
