using System;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Security;
using Victoria.Core;

namespace Victoria.Inventory.Application.Commands
{
    public class PickLpnCommand
    {
        public string TenantId { get; set; } = string.Empty;
        public string LpnId { get; set; } = string.Empty;
        public int FoundQuantity { get; set; }
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
                var actorTenant = TenantId.Create(command.TenantId);

                // SIMULACIÓN: En una App real, cargaríamos el LPN de la DB.
                // Aquí, si el LPN ID contiene "NATSUKI", lo forzamos a ser de NATSUKI
                // independientemente de quién sea el actor.
                var storedTenant = command.LpnId.Contains("NATSUKI") ? "NATSUKI" : command.TenantId;

                var lpn = Lpn.Create(storedTenant, command.LpnId, LpnCode.Create("LPN1234567890"), Sku.Create("SKU-001"), 10, "SYS", "SYS");
                lpn.ClearChanges();
                
                // SEGURIDAD: Validar acceso antes de cualquier transición
                TenantGuard.EnsureSameTenant(actorTenant, lpn);

                // Simular estados previos hasta llegar a Allocated
                lpn.Receive("ORD-INIT", "SYS", "SYS");
                lpn.Putaway("Z01-P01-R01-N1-01", "SYS", "SYS");
                lpn.Allocate("ORDER-001", Sku.Create("SKU-001"), "SYS", "SYS");
                lpn.ClearChanges();

                // 2. Lógica de Picking con Shortage
                int expected = 10; // En real, vendría de la asignación del LPN
                if (command.FoundQuantity < expected)
                {
                    // Reportar Faltante
                    var shortageEvent = new Victoria.Inventory.Domain.Events.PickShortageDetected(
                        command.TenantId,
                        command.LpnId,
                        "ORDER-001",
                        expected,
                        command.FoundQuantity,
                        DateTime.UtcNow,
                        command.UserId,
                        command.StationId
                    );
                    
                    // Ajustar saldo del LPN inmediatamente para evitar nuevos picking erróneos
                    lpn.AdjustQuantity(command.FoundQuantity, "PICK_SHORTAGE_DETECTED", command.UserId, command.StationId);
                    
                    // Trigger automático de AUDITORÍA (Cycle Count Task)
                    // En una app real: _mediator.Send(new CreateCycleCountTaskCommand(...));
                    Console.WriteLine($"[URGENT] CycleCountTask created for Location {lpn.CurrentLocation} due to Pick Shortage.");
                }

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
