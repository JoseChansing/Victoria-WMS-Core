using System;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;

namespace Victoria.Inventory.Application.Commands
{
    public class ReceiveLpnCommand
    {
        public string TenantId { get; set; } = string.Empty;
        public string LpnId { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
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
                throw new InvalidOperationException($"Could not acquire lock for LPN {command.LpnId}");

            try
            {
                // In real app: Load aggregate from event store
                // var events = await _eventStore.GetEventsAsync(command.LpnId);
                // var lpn = Lpn.Load(events);
                
                // For Walking Skeleton, we simulate a fresh aggregate or simple load
                // Un LPN nace con un Tenant asignado
                var lpn = Lpn.Create(
                    command.TenantId,
                    command.LpnId, 
                    LpnCode.Create("LPN1234567890"), 
                    Sku.Create("SKU-001"), 
                    10, 
                    command.UserId, 
                    command.StationId);
                
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
