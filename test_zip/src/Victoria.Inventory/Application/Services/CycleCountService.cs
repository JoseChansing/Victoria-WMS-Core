using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Victoria.Core;
using Victoria.Core.Infrastructure;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Security;

namespace Victoria.Inventory.Application.Services
{
    public class CycleCountService
    {
        private readonly IEventStore _eventStore;
        private readonly ILockService _lockService;
        private const int DiscrepancyThreshold = 5; // Unidades
        private const double TolerancePercent = 0.05; // 5%

        public CycleCountService(IEventStore eventStore, ILockService lockService)
        {
            _eventStore = eventStore;
            _lockService = lockService;
        }

        public async Task ProcessBlindCount(string tenantId, string lpnId, int countedQuantity, string userId, string stationId)
        {
            var actorTenant = TenantId.Create(tenantId);
            var lockKey = $"LOCK:LPN:{lpnId}";

            if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30)))
                throw new InvalidOperationException("Could not lock LPN for counting");

            try
            {
                // 1. Cargar LPN (Simulado con Tenancy)
                var lpn = Lpn.Create(tenantId, lpnId, LpnCode.Create("LPN1234567890"), Sku.Create("SKU-001"), 100, userId, stationId);
                lpn.ClearChanges();
                lpn.Receive("ORD-INIT", "SYS", "SYS");
                lpn.Putaway("ZONE-A", "SYS", "SYS");
                lpn.ClearChanges();

                // SEGURIDAD
                TenantGuard.EnsureSameTenant(actorTenant, lpn);

                // 2. Registrar el Conteo
                lpn.ReportCount(countedQuantity, userId, stationId);

                // 3. Evaluar Discrepancia
                int discrepancy = Math.Abs(lpn.Quantity - countedQuantity);
                bool requiresApproval = discrepancy > DiscrepancyThreshold || (discrepancy / (double)lpn.Quantity) > TolerancePercent;

                if (requiresApproval)
                {
                    // Bloquear LPN
                    lpn.Quarantine($"High discrepancy detected ({discrepancy} units). Reported: {countedQuantity}, Expected: {lpn.Quantity}", userId, stationId);
                }
                else
                {
                    // Ajuste Automático
                    lpn.AdjustQuantity(countedQuantity, "AUTO_ADJUST_LOW_DISCREPANCY", userId, stationId);
                }

                await _eventStore.AppendEventsAsync(lpnId, -1, lpn.Changes);
            }
            finally
            {
                await _lockService.ReleaseLockAsync(lockKey);
            }
        }

        public async Task AuthorizeAdjustment(string tenantId, string lpnId, int newQuantity, string reason, string supervisorId)
        {
            // Simulación de validación de Rol (En una App real vendría del ClaimsPrincipal o IAuthorizationService)
            if (!supervisorId.Contains("SUPER") && !supervisorId.Contains("ADMIN"))
            {
                throw new UnauthorizedAccessException("Only Supervisors or Admins can authorize manual adjustments.");
            }

            var actorTenant = TenantId.Create(tenantId);
            var lockKey = $"LOCK:LPN:{lpnId}";

            if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30)))
                throw new InvalidOperationException("Could not lock LPN for adjustment");

            try
            {
                // Cargar LPN...
                var lpn = Lpn.Create(tenantId, lpnId, LpnCode.Create("LPN1234567890"), Sku.Create("SKU-001"), 100, supervisorId, "AUTH-STATION");
                lpn.ClearChanges();

                TenantGuard.EnsureSameTenant(actorTenant, lpn);

                lpn.AdjustQuantity(newQuantity, reason, supervisorId, "AUTH-STATION");
                // Podríamos añadir un ReleaseQuarantine aquí si el estado fuera Quarantine
                
                await _eventStore.AppendEventsAsync(lpnId, -1, lpn.Changes);
            }
            finally
            {
                await _lockService.ReleaseLockAsync(lockKey);
            }
        }
    }
}
