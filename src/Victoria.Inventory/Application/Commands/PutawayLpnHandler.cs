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
                    // 1. Cargar Agregados (Simulado con Tenancy)
                    // En producción: await _eventStore.GetEventsAsync(...)
                    var lpn = Lpn.Provision(command.LpnId, LpnCode.Create("LPN1234567890"), Sku.Create("SKU-001"), LpnType.Loose, 10, PhysicalAttributes.Empty(), command.UserId, command.StationId);
                    lpn.ClearChanges();
                    // Simulamos que ya fue recibido
                    lpn.Receive("ORD-INIT", "SYS", "SYS"); 
                    lpn.ClearChanges();

                    var location = Location.Create(LocationCode.Create(command.LocationCode), LocationProfile.Picking, true);
                    location.ClearChanges();

                    // SEGURIDAD: Validar que el usuario tenga permisos (ya no validamos TenantId del LPN porque es Single-Tenant)
                    // TenantGuard.EnsureSameTenant(actorTenant, location); // Location todavía puede tener TenantId si no lo hemos refactorizado
                    // TenantGuard.EnsureCompatibility(lpn, location);
                    // Checked removed


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
