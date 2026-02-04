using System;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;

namespace Victoria.Inventory.Application.Commands
{
    public class PickLpnCommand
    {
        public string LpnId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
    }

    public class PickLpnHandler
    {
        private readonly IEventStore _eventStore;
        private readonly ILockService _lockService;

        public PickLpnHandler(IEventStore eventStore, ILockService lockService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
        }

        public async Task Handle(PickLpnCommand command)
        {
            var lockKey = $"LOCK:LPN:{command.LpnId}";
            if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30)))
                throw new InvalidOperationException($"Could not acquire lock for LPN {command.LpnId}");

            try
            {
                // 1. Cargar Agregado (Simulado)
                var lpn = Lpn.Create(command.LpnId, LpnCode.Create("LPN1234567890"), Sku.Create("SKU-001"), 10, "SYS", "SYS");
                lpn.ClearChanges();
                
                // Simular estados previos hasta llegar a Allocated
                lpn.Receive("ORD-INIT", "SYS", "SYS");
                lpn.Putaway("Z01-P01-R01-N1-01", "SYS", "SYS");
                lpn.Allocate("ORDER-001", Sku.Create("SKU-001"), "SYS", "SYS");
                lpn.ClearChanges();

                // 2. Lógica de Picking
                lpn.Pick(command.UserId, command.StationId);

                // 3. Persistencia
                await _eventStore.AppendEventsAsync(command.LpnId, -1, lpn.Changes);
            }
            finally
            {
                await _lockService.ReleaseLockAsync(lockKey);
            }
        }
    }
}
