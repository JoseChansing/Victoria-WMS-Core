using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Victoria.Core.Interfaces;
using Victoria.Infrastructure.Integration.Odoo;

namespace Victoria.Infrastructure.Services
{
    public class RecurringSyncWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RecurringSyncWorker> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(15); // optimized for dev

        public RecurringSyncWorker(IServiceProvider serviceProvider, ILogger<RecurringSyncWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        private int _guardianCycleCounter = 0;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"[WORKER] Recurring Product Sync Worker started. Interval: {_interval.TotalSeconds} sec.");

            // Initial wait to let InitialDataLoader finish first if needed
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("[WORKER] Starting scheduled incremental sync...");
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                        var inboundService = scope.ServiceProvider.GetRequiredService<IInboundService>();
                        var odooClient = scope.ServiceProvider.GetRequiredService<IOdooRpcClient>();

                        // 1. SYNC INBOUND ORDERS (PRIORITY)
                        try 
                        {
                            _logger.LogInformation("[WORKER] Syncing Inbound Orders...");
                            int orderCount = await inboundService.SyncAllAsync(odooClient);
                            
                            _logger.LogInformation("[WORKER] Executing Sync Guardian (Consistency Check)...");
                            int guardianActions = await inboundService.PerformCleanupGuardian(odooClient);
                            _logger.LogInformation($"[WORKER] Inbound Sync finished. Orders: {orderCount}, Guardian Actions: {guardianActions}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[WORKER] Error during Inbound Order sync. Continuing to Products...");
                        }

                        // 2. SYNC PRODUCTS (BACKGROUND)
                        try 
                        {
                            _logger.LogInformation("[WORKER] Syncing Products...");
                            int prodCount = await productService.SyncAllAsync(odooClient);
                            _logger.LogInformation($"[WORKER] Product sync finished. Total: {prodCount}.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[WORKER] Error during Product sync.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WORKER] Error during recurring sync.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
