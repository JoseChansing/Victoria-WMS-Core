using System;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;

namespace Victoria.Inventory.Application.Commands
{
    public class PutawayLpnCommand
    {
        public string LpnId { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
    }

    public class PutawayLpnHandler
    {
        private readonly IEventStore _eventStore;
        private readonly ILockService _lockService;

        public PutawayLpnHandler(IEventStore eventStore, ILockService lockService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
        }

        public async Task Handle(PutawayLpnCommand command)
        {
            var lpnLockKey = $"LOCK:LPN:{command.LpnId}";
            var locLockKey = $"LOCK:LOC:{command.LocationCode}";

            // REQUISITO CRÍTICO: Doble Bloqueo Secuencial
            if (!await _lockService.AcquireLockAsync(lpnLockKey, TimeSpan.FromSeconds(30)))
                throw new InvalidOperationException($"Could not acquire lock for LPN {command.LpnId}");

            try
            {
                if (!await _lockService.AcquireLockAsync(locLockKey, TimeSpan.FromSeconds(30)))
                    throw new InvalidOperationException($"Could not acquire lock for Location {command.LocationCode}");

                try
                {
                    // 1. Cargar Agregados (Simulado para el Walking Skeleton)
                    // En producción: await _eventStore.GetEventsAsync(...)
                    var lpn = Lpn.Create(command.LpnId, LpnCode.Create("LPN1234567890"), Sku.Create("SKU-001"), 10, "ROOT", "INIT");
                    lpn.ClearChanges();
                    // Simulamos que ya fue recibido
                    lpn.Receive("ORD-MOCK", "SYS", "SYS"); 
                    lpn.ClearChanges();

                    var location = Location.Create(LocationCode.Create(command.LocationCode), command.UserId, command.StationId);
                    location.ClearChanges();

                    // 2. Ejecutar Lógica de Negocio (Coordinada)
                    lpn.Putaway(command.LocationCode, command.UserId, command.StationId);
                    location.AssignLpn(LpnCode.Create("LPN-TEST-001"), command.UserId, command.StationId);

                    // 3. REQUISITO DE AUDITORÍA: Persistencia Atómica Multi-Stream
                    await _eventStore.SaveBatchAsync(new[]
                    {
                        new EventStreamBatch(command.LpnId, -1, lpn.Changes),
                        new EventStreamBatch(command.LocationCode, -1, location.Changes)
                    });
                }
                finally
                {
                    await _lockService.ReleaseLockAsync(locLockKey);
                }
            }
            finally
            {
                await _lockService.ReleaseLockAsync(lpnLockKey);
            }
        }
    }
}
