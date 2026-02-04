using System;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Security;
using Victoria.Core;

namespace Victoria.Inventory.Application.Commands
{
    public class PutawayLpnCommand
    {
        public string TenantId { get; set; } = string.Empty;
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
                throw new InvalidOperationException("Could not lock LPN");

            try
            {
                if (!await _lockService.AcquireLockAsync(locLockKey, TimeSpan.FromSeconds(30)))
                    throw new InvalidOperationException("Could not lock Location");

                try
                {
                    var actorTenant = TenantId.Create(command.TenantId);

                    // 1. Cargar Agregados (Simulado con Tenancy)
                    // En producción: await _eventStore.GetEventsAsync(...)
                    var lpn = Lpn.Create(command.TenantId, command.LpnId, LpnCode.Create("LPN1234567890"), Sku.Create("SKU-001"), 10, command.UserId, command.StationId);
                    lpn.ClearChanges();
                    // Simulamos que ya fue recibido
                    lpn.Receive("ORD-INIT", "SYS", "SYS"); 
                    lpn.ClearChanges();

                    var location = Location.Create(command.TenantId, LocationCode.Create(command.LocationCode));
                    location.ClearChanges();

                    // SEGURIDAD: Validar que el actor pertenezca al Tenant del LPN y la Ubicación
                    TenantGuard.EnsureSameTenant(actorTenant, lpn);
                    TenantGuard.EnsureSameTenant(actorTenant, location);
                    TenantGuard.EnsureCompatibility(lpn, location);

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
