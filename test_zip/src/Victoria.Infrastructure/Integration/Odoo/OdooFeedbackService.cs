using System;
using System.Linq;
using System.Threading.Tasks;
using Victoria.Core.Messaging;
using Victoria.Inventory.Domain.Events;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooFeedbackService
    {
        private readonly IMessageBus _bus;

        public OdooFeedbackService(IMessageBus bus)
        {
            _bus = bus;
        }

        public async Task ReportDispatch(DispatchConfirmed @event)
        {
            // Mapeo de Victoria Dispatch -> Odoo Delivery Order Validation
            var odooMessage = new 
            {
                OdooOrderId = @event.OrderId,
                Date = @event.OccurredOn,
                Items = @event.DispatchedLpnIds.Select(id => new { LpnId = id, Status = "SHIPPED" }),
                Tenant = @event.TenantId
            };

            await _bus.PublishAsync(odooMessage);
            Console.WriteLine($"[ACL] Dispatch Feedback sent to Odoo for Order {@event.OrderId}");
        }

        public async Task ReportOverage(PickingOverageApproved @event)
        {
            // Crucial: Notificar a Odoo que se despachó DE MÁS
            var odooAdjustment = new
            {
                Type = "OVERAGE_ADJUSTMENT",
                Order = @event.OrderId,
                Line = @event.LineId,
                NewQuantity = @event.NewQuantity,
                AuthorizedBy = @event.SupervisorId,
                Reason = @event.ReasonCode
            };

            await _bus.PublishAsync(odooAdjustment);
            Console.WriteLine($"[ACL] Overage Adjustment reported to Odoo: {@event.OrderId} -> {@event.NewQuantity} units.");
        }
    }
}
