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
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5); // 5 Minutes Interval

        public RecurringSyncWorker(IServiceProvider serviceProvider, ILogger<RecurringSyncWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"[WORKER] Recurring Product Sync Worker started. Interval: {_interval.TotalMinutes} min.");

            // Initial wait to let InitialDataLoader finish first if needed
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("[WORKER] Starting scheduled incremental sync...");
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var productService = scope.ServiceProvider.GetRequiredService<ProductSyncService>();
                        var odooClient = scope.ServiceProvider.GetRequiredService<IOdooRpcClient>();

                        int count = await productService.SyncAllAsync(odooClient);
                        _logger.LogInformation($"[WORKER] Incremental sync finished. Processed {count} items.");
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
