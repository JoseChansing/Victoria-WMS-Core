using System;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.Events;

namespace Victoria.Inventory.Application.Commands
{
    public record RequestPickingOverageCommand(
        string TenantId,
        string OrderId,
        string LineId,
        int OrderedQuantity,
        int ScannedQuantity,
        string UserId,
        string StationId
    );

    public class RequestPickingOverageHandler
    {
        private readonly IEventStore _eventStore;
        private readonly ILockService _lockService;

        public RequestPickingOverageHandler(IEventStore eventStore, ILockService lockService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
        }

        public async Task Handle(RequestPickingOverageCommand command)
        {
            var lockKey = $"LOCK:ORDER:{command.OrderId}";
            if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30)))
                throw new InvalidOperationException("Could not lock order for overage request");

            try
            {
                // SIMULACIÓN: Cargar Orden
                var order = new OutboundOrder(command.TenantId, command.OrderId);
                order.AddLine(command.LineId, "SKU-PROMO", command.OrderedQuantity);

                // Lógica de Negocio
                order.RequestOverage(command.LineId, command.ScannedQuantity);

                // Emitir Evento
                var @event = new PickingOverageRequested(
                    command.TenantId,
                    command.OrderId,
                    command.LineId,
                    "SKU-PROMO",
                    command.OrderedQuantity,
                    command.ScannedQuantity,
                    DateTime.UtcNow,
                    command.UserId,
                    command.StationId
                );

                await _eventStore.AppendEventsAsync(command.OrderId, -1, new[] { @event });
                
                Console.WriteLine($"[OVERAGE REQUESTED] Order: {command.OrderId}, Line: {command.LineId}, Scanned: {command.ScannedQuantity}");
            }
            finally
            {
                await _lockService.ReleaseLockAsync(lockKey);
            }
        }
    }
}
