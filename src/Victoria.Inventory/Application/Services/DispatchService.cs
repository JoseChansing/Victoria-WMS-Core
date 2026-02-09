using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.Events;
using Victoria.Inventory.Domain.Security;
using Victoria.Inventory.Domain.Services;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Core;

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

        public async Task<string> DispatchOrder(string tenantId, string orderId, string dockDoor, string userId)
        {
            var actorTenant = TenantId.Create(tenantId);
            
            var lpnsToShip = new List<string> { "LPN-TEST-001", "MST-TEST-001" }; // Simulación
            
            var batches = new List<EventStreamBatch>();
            
            foreach(var lpnId in lpnsToShip)
            {
                // Bloquear y transicionar (Simulado con Tenancy)
                // Bloquear y transicionar (Simulado con Tenancy)
                var lpn = Lpn.Provision(lpnId, Victoria.Inventory.Domain.ValueObjects.LpnCode.Create("LPN1234567890"), Victoria.Inventory.Domain.ValueObjects.Sku.Create("SKU-001"), LpnType.Loose, 10, PhysicalAttributes.Empty(), userId, "DISPATCH");
                lpn.ClearChanges();

                // SEGURIDAD
                // Checked removed

                lpn.Ship(userId);
                batches.Add(new EventStreamBatch(lpnId, -1, lpn.Changes));
            }

            // Evento de Cierre de Negocio con TenantId
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
