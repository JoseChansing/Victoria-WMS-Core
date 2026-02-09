using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Victoria.Inventory.Domain.Events;
using Victoria.Core.Messaging;
using Victoria.Core.Interfaces;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooFeedbackConsumer
    {
        private readonly IOdooRpcClient _odooClient;
        private readonly Microsoft.Extensions.Logging.ILogger<OdooFeedbackConsumer> _logger;

        public OdooFeedbackConsumer(IOdooRpcClient odooClient, Microsoft.Extensions.Logging.ILogger<OdooFeedbackConsumer> logger)
        {
            _odooClient = odooClient;
            _logger = logger;
        }

        public async Task Handle(DispatchConfirmed @event)
        {
            try
            {
                // 1. Buscar el stock.picking en Odoo por el OrderId de Victoria
                // var pickingIds = await _odooClient.SearchAsync("stock.picking", [["origin", "=", @event.OrderId]]);
                
                // 2. Ejecutar validación en Odoo
                Console.WriteLine($"[ACL-OUT] Validating Picking in Odoo for Victoria Order: {@event.OrderId}");
                
                // Simulación de llamada remota para cerrar el albarán
                bool success = await _odooClient.ExecuteAsync("stock.picking", "button_validate", new object[] { 123 });
                
                if (!success) throw new Exception("Odoo button_validate returned false.");
            }
            catch (Exception ex)
            {
                // REQUERIMIENTO 3: EL BOTÓN DE LA MUERTE (MANEJO DE ERRORES)
                Console.WriteLine($"[CRITICAL] Odoo Sync Failed for {@event.OrderId}: {ex.Message}");
                
                // En una app real:
                // var order = await _orderRepository.GetByIdAsync(@event.OrderId);
                // order.MarkSyncError(ex.Message);
                // await _orderRepository.UpdateAsync(order);
                
                // Loguear para Torre de Control
                _logger.LogError("FATAL: Victoria confirmed dispatch but Odoo is out of sync for Order {Order}", @event.OrderId);
            }
        }
    }
}
