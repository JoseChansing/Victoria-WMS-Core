using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Victoria.Core.Interfaces;
using Victoria.Core.Messaging;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooPollingService : BackgroundService
    {
        private readonly ILogger<OdooPollingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private DateTime _lastSync = DateTime.UtcNow.AddDays(-1);

        public OdooPollingService(
            ILogger<OdooPollingService> logger, 
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[Odoo Sync] Service Started. Polling every 60 seconds.");
            _logger.LogInformation("Odoo Polling Service is starting (Single-Tenant Mode).");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var odooClient = scope.ServiceProvider.GetRequiredService<IOdooRpcClient>();
                        var productSync = scope.ServiceProvider.GetRequiredService<ProductSyncService>();
                        var orderSync = scope.ServiceProvider.GetRequiredService<InboundOrderSyncService>();
                        var outboundSync = scope.ServiceProvider.GetRequiredService<Victoria.Inventory.Application.Services.OutboundOrderSyncService>();

                        Console.WriteLine($"[WORKER] Starting Sync Cycle...");
                        
                        // 1. Products
                        await productSync.SyncAllAsync(odooClient);
                        
                        // 2. Inbound Orders
                        await orderSync.SyncAllAsync(odooClient);

                        // 3. Outbound Orders (Phase 4)
                        await outboundSync.SyncOrdersAsync();

                        Console.WriteLine("[WORKER] Sync Cycle Completed.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WORKER ERROR] {ex.Message}");
                    _logger.LogError(ex, "Error occurred during Odoo polling.");
                }

                Console.WriteLine($"💓 [POLLING] Escaneando Odoo... (Próximo: +5m)");
                await Task.Delay(300000, stoppingToken);
            }
        }
    }
}
