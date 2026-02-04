using System;
using System.Text.Json;
using Victoria.Inventory.Domain.Events;

namespace Victoria.Infrastructure.Integration
{
    // Simulación de un receptor de eventos de integración
    // En el futuro esto enviaría a RabbitMQ, Azure Service Bus, etc.
    public class DispatchEventHandler
    {
        public void Handle(DispatchConfirmed @event)
        {
            // REQUISITO: Simular integración con Odoo
            var odooDto = new
            {
                Operation = "WMS_DISPATCH_CONFIRMED",
                ExternalOrderId = @event.OrderId,
                Dock = @event.DockDoor,
                Timestamp = @event.OccurredOn,
                ItemsCount = @event.DispatchedLpnIds.Count(),
                SourceSystem = "VictoriaCore"
            };

            var json = JsonSerializer.Serialize(odooDto, new JsonSerializerOptions { WriteIndented = true });
            
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("INTEGRATION ALERT: Sending Dispatch to Odoo...");
            Console.WriteLine(json);
            Console.WriteLine("--------------------------------------------------");
        }
    }
}
