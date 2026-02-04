using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;

namespace Victoria.Inventory.Domain.Services
{
    public class AllocationService
    {
        private readonly IEventStore _eventStore;
        private readonly ILockService _lockService;

        public AllocationService(IEventStore eventStore, ILockService lockService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
        }

        public async Task AllocateStockForOrder(string orderId, Sku sku, int quantity, string userId, string stationId)
        {
            // REQUISITO: Buscar LPNs disponibles (Simulación para el Walking Skeleton)
            // En un entorno real, usarías una Proyección de Inventario (Read Model) en PostgreSQL
            // Aquí simulamos que encontramos los LPNs necesarios.
            
            var candidateLpns = new List<string> { "LPN-TEST-001" }; // Supongamos que este es el que movimos en Fase 6
            
            var batches = new List<EventStreamBatch>();
            var acquiredLocks = new List<string>();

            try
            {
                foreach (var lpnId in candidateLpns)
                {
                    var lockKey = $"LOCK:LPN:{lpnId}";
                    if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10)))
                        throw new InvalidOperationException($"Could not lock LPN {lpnId} for allocation.");
                    
                    acquiredLocks.Add(lockKey);

                    // Cargar Agregado (Simulado)
                    var lpn = Lpn.Create(lpnId, LpnCode.Create("LPN1234567890"), sku, 10, "SYS", "SYS");
                    lpn.ClearChanges();
                    
                    // Simular que ya fue recibido y ubicado para poder reservarlo
                    lpn.Receive("ORD-INIT", "SYS", "SYS");
                    lpn.Putaway("Z01-P01-R01-N1-01", "SYS", "SYS");
                    lpn.ClearChanges();

                    // Intentar Reservar
                    lpn.Allocate(orderId, sku, userId, stationId);

                    batches.Add(new EventStreamBatch(lpnId, -1, lpn.Changes));
                }

                // ATOMICIDAD CRÍTICA: Todo o nada usando SaveBatchAsync
                await _eventStore.SaveBatchAsync(batches);
            }
            finally
            {
                // Liberar Locks
                foreach (var lockKey in acquiredLocks)
                {
                    await _lockService.ReleaseLockAsync(lockKey);
                }
            }
        }
    }
}
