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
        public int ExpectedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
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
                    command.ReceivedQuantity, 
                    command.UserId, 
                    command.StationId);
                
                // DETECCIÓN DE OVERAGE
                if (command.ReceivedQuantity > command.ExpectedQuantity)
                {
                    lpn.Quarantine(
                        $"OVERAGE_PENDING_APPROVAL: Expected {command.ExpectedQuantity}, Received {command.ReceivedQuantity}", 
                        command.UserId, 
                        command.StationId);
                }
                else if (command.ReceivedQuantity < command.ExpectedQuantity)
                {
                    // Lógica de Shortage: Emitir evento especializado
                    // En una app real, esto podría disparar un flujo en el ERP/Odoo
                    var shortageEvent = new Victoria.Inventory.Domain.Events.ReceiptClosedWithShortage(
                        command.TenantId,
                        command.OrderId,
                        "SKU-001",
                        command.ExpectedQuantity,
                        command.ReceivedQuantity,
                        DateTime.UtcNow,
                        command.UserId,
                        command.StationId
                    );
                    
                    // Aquí podríamos persistir este evento por separado o añadirlo al stream
                    // Por ahora, solo cumplimos con la lógica de negocio técnica
                }
                
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
