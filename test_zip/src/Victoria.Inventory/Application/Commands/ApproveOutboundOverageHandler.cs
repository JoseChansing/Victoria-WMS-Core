using System;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.Events;

namespace Victoria.Inventory.Application.Commands
{
    public record ApproveOutboundOverageCommand(
        string TenantId,
        string OrderId,
        string LineId,
        int ApprovedQuantity,
        string SupervisorId,
        string ReasonCode,
        string StationId
    );

    public class ApproveOutboundOverageHandler
    {
        private readonly IEventStore _eventStore;
        private readonly ILockService _lockService;

        public ApproveOutboundOverageHandler(IEventStore eventStore, ILockService lockService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
        }

        public async Task Handle(ApproveOutboundOverageCommand command)
        {
            var lockKey = $"LOCK:ORDER:{command.OrderId}";
            if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30)))
                throw new InvalidOperationException("Could not lock order for overage approval");

            try
            {
                // SIMULACIÓN: Cargar Orden
                var order = new OutboundOrder(command.TenantId, command.OrderId);
                order.AddLine(command.LineId, "SKU-PROMO", 10); // Supongamos que pedía 10
                order.RequestOverage(command.LineId, command.ApprovedQuantity);

                // Lógica de Aprobación
                order.ApproveOverage(command.LineId, command.ApprovedQuantity);

                // Emitir Evento Crítico
                var @event = new PickingOverageApproved(
                    command.TenantId,
                    command.OrderId,
                    command.LineId,
                    command.ApprovedQuantity,
                    command.SupervisorId,
                    command.ReasonCode,
                    DateTime.UtcNow,
                    command.SupervisorId,
                    command.StationId
                );

                // Persistencia e integración (Odoo recibiría este evento en Fase 14)
                await _eventStore.AppendEventsAsync(command.OrderId, -1, new[] { @event });
                
                Console.WriteLine($"[OVERAGE APPROVED] Order {command.OrderId} updated to {command.ApprovedQuantity} units. Reason: {command.ReasonCode}");
            }
            finally
            {
                await _lockService.ReleaseLockAsync(lockKey);
            }
        }
    }
}
