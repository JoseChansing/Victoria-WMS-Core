using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Events;
using Victoria.Inventory.Domain.Security;
using Victoria.Core;

namespace Victoria.Inventory.Application.Commands
{
    public class PackLpnsCommand
    {
        public string TenantId { get; set; } = string.Empty;
        public string MasterLpnId { get; set; } = string.Empty;
        public List<string> ChildLpnIds { get; set; } = new();
        public double Weight { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
    }

    public class PackingHandler
    {
        private readonly IEventStore _eventStore;
        private readonly ILockService _lockService;

        public PackingHandler(IEventStore eventStore, ILockService lockService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
        }

        public async Task Handle(PackLpnsCommand command)
        {
            var masterLock = $"LOCK:LPN:{command.MasterLpnId}";
            if (!await _lockService.AcquireLockAsync(masterLock, TimeSpan.FromSeconds(30)))
                throw new InvalidOperationException("Could not lock master LPN");

            try
            {
                var batches = new List<EventStreamBatch>();
                var actorTenant = TenantId.Create(command.TenantId);

                // 1. Crear el Contenedor Maestro con Tenant
                var masterLpn = Lpn.Create(command.TenantId, command.MasterLpnId, LpnCode.Create("LPN9990000001"), Sku.Create("CONTAINER-01"), 1, command.UserId, command.StationId);
                masterLpn.ClearChanges();

                var packingEvent = new PackingCompleted(
                    command.TenantId,
                    command.MasterLpnId,
                    command.ChildLpnIds,
                    command.Weight,
                    DateTime.UtcNow,
                    command.UserId,
                    command.StationId
                );
                
                // Aplicar a nivel interno (simulado para este skeleton)
                batches.Add(new EventStreamBatch(command.MasterLpnId, -1, new[] { packingEvent }));

                // 2. Vincular Hijos (Actualizar cada LPN hijo)
                foreach(var childId in command.ChildLpnIds)
                {
                    // En producción: cargar aggregate, set parent, persistir
                    // batches.Add(new EventStreamBatch(childId, -1, new[]{ new LpnLinkedToMaster(...) }));
                }

                await _eventStore.SaveBatchAsync(batches);
            }
            finally
            {
                await _lockService.ReleaseLockAsync(masterLock);
            }
        }
    }
}
