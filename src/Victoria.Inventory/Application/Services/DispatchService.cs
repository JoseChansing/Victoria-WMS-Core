using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.Events;
using Victoria.Inventory.Domain.Services;

namespace Victoria.Inventory.Application.Services
{
    public class DispatchService
    {
        private readonly IEventStore _eventStore;
        private readonly ILockService _lockService;
        private readonly LabelService _labelService;

        public DispatchService(IEventStore eventStore, ILockService lockService, LabelService labelService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
            _labelService = labelService;
        }

        public async Task<string> DispatchOrder(string orderId, string dockDoor, string userId)
        {
            // REQUISITO: Validar Orden Completa
            // En producción: Consultar base de datos para ver si todos los SKUs de la orden tienen LPNs en 'Picked'
            
            var lpnsToShip = new List<string> { "LPN-TEST-001", "MST-TEST-001" }; // Simulación
            
            var batches = new List<EventStreamBatch>();
            
            foreach(var lpnId in lpnsToShip)
            {
                // Bloquear y transicionar
                var lpn = Lpn.Create(lpnId, Victoria.Inventory.Domain.ValueObjects.LpnCode.Create("LPN1234567890"), Victoria.Inventory.Domain.ValueObjects.Sku.Create("SKU-001"), 10, userId, "DISPATCH");
                lpn.ClearChanges();
                lpn.Ship(userId);
                
                batches.Add(new EventStreamBatch(lpnId, -1, lpn.Changes));
            }

            // Evento de Cierre de Negocio
            var dispatchEvent = new DispatchConfirmed(
                orderId,
                dockDoor,
                lpnsToShip,
                DateTime.UtcNow,
                userId,
                "DISPATCH-STATION"
            );
            
            batches.Add(new EventStreamBatch($"ORDER:{orderId}", -1, new[] { dispatchEvent }));

            await _eventStore.SaveBatchAsync(batches);

            // Generar Etiqueta ZPL para el contenedor principal
            return _labelService.GenerateShippingLabelZpl("MST-TEST-001", orderId, "CLIENT-A / DOCK-" + dockDoor);
        }
    }
}
