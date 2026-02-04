using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Victoria.Core.Messaging;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooPollingService : BackgroundService
    {
        private readonly ILogger<OdooPollingService> _logger;
        private readonly IOdooRpcClient _odooClient;
        private readonly IMessageBus _bus;
        private DateTime _lastSync = DateTime.UtcNow.AddDays(-1);

        public OdooPollingService(ILogger<OdooPollingService> logger, IOdooRpcClient odooClient, IMessageBus bus)
        {
            _logger = logger;
            _odooClient = odooClient;
            _bus = bus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Odoo Polling Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // REQUERIMIENTO 1: PERSISTENCIA DEL POLLING
                    // En una app real: var state = await _repository.GetAsync("GLOBAL", "ODOO_SYNC");
                    // _lastSync = state.LastSyncDate;
                    
                    _logger.LogInformation("Polling Odoo for changes since {LastSync}", _lastSync);

                    // 1. Sync Products (REQUERIMIENTO 2: SCOPE MULTI-TENANT)
                    await SyncProducts("PERFECTPTY", 1);
                    await SyncProducts("NATSUKI", 2);

                    // 2. Sync Orders
                    await SyncOrders();

                    _lastSync = DateTime.UtcNow;
                    // await _repository.UpdateAsync("GLOBAL", "ODOO_SYNC", _lastSync);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Odoo polling.");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        private async Task SyncProducts(string tenantId, int odooCompanyId)
        {
            _logger.LogInformation("[POLLING] Syncing Products for Tenant {Tenant} (Odoo Company {Id})", tenantId, odooCompanyId);
            // var products = await _odooClient.SearchAndReadAsync("product.product", domain_with_company, fields);
            await Task.CompletedTask;
        }

        private async Task SyncOrders()
        {
            // Simulación de detección de órdenes nuevas
            await Task.CompletedTask;
        }
    }
}
