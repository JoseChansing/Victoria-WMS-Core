using System;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.Inventory.Application.Commands
{
    public class ApproveReceiptOverageCommand
    {
        public string TenantId { get; set; } = string.Empty;
        public string LpnId { get; set; } = string.Empty;
        public string SupervisorId { get; set; } = string.Empty;
    }

    public class ApproveReceiptOverageHandler
    {
        private readonly Victoria.Core.Infrastructure.IEventStore _eventStore;

        public ApproveReceiptOverageHandler(Victoria.Core.Infrastructure.IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public async Task Handle(ApproveReceiptOverageCommand command)
        {
            // Lógica: Sacar LPN de Quarantine y pasarlo a Putaway (o Received listo para Putaway)
            Console.WriteLine($"[SUPERVISOR] Overage Approved by {command.SupervisorId} for LPN {command.LpnId}");
            
            // Simulación de carga y cambio de estado
            // var lpn = Load(command.LpnId);
            // lpn.ApproveOverage(command.SupervisorId);
            // await _eventStore.AppendEventsAsync(...);
            
            await Task.CompletedTask;
        }
    }
}
