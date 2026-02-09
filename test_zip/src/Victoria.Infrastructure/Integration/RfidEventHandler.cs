using System;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Events;

namespace Victoria.Infrastructure.Integration
{
    public class RfidEventHandler
    {
        public Task Handle(RfidMismatchDetected @event)
        {
            // En una implementación real, esto enviaría una alerta a un Dashboard (SignalR),
            // crearía una tarea de reconteo o bloquearía la ubicación.
            
            Console.WriteLine($"[RFID ALERT] Mismatch detected at {@event.LocationCode} (Tenant: {@event.TenantId})");
            Console.WriteLine($"Missing Tags: {string.Join(", ", @event.MissingEpcs)}");
            Console.WriteLine($"Extra Tags: {string.Join(", ", @event.ExtraEpcs)}");
            Console.WriteLine($"Reported by: {@event.CreatedBy} from {@event.StationId}");
            
            return Task.CompletedTask;
        }
    }
}
