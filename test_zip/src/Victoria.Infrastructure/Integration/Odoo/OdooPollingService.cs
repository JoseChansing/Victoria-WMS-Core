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
        private readonly ProductSyncService _productSync;
        private readonly InboundOrderSyncService _orderSync;
        private DateTime _lastSync = DateTime.UtcNow.AddDays(-1);

        public OdooPollingService(
            ILogger<OdooPollingService> logger, 
            IOdooRpcClient odooClient, 
            IMessageBus bus,
            ProductSyncService productSync,
            InboundOrderSyncService orderSync)
        {
            _logger = logger;
            _odooClient = odooClient;
            _bus = bus;
            _productSync = productSync;
            _orderSync = orderSync;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Odoo Polling Service is starting (Single-Tenant Mode).");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Polling Odoo for changes since {LastSync}", _lastSync);

                    // 1. Sync Products (Instance-Agnostic)
                    await SyncProducts();

                    // 2. Sync Orders
                    await SyncOrders();

                    _lastSync = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Odoo polling.");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        private async Task SyncProducts()
        {
            _logger.LogInformation("[POLLING] Syncing Products...");
            
            var domain = new object[][] { 
                new object[] { "active", "=", true }
            };

            var fields = new string[] { 
                "id", "display_name", "default_code", "weight", 
                "image_128"
            };

            var products = await _odooClient.SearchAndReadAsync<OdooProductDto>("product.product", domain, fields);
            _logger.LogInformation("[POLLING] Odoo returned {Count} products", products.Count);
            
            foreach (var p in products)
            {
                await _productSync.SyncProduct(p);
            }
        }

        private async Task SyncOrders()
        {
            _logger.LogInformation("[POLLING] Syncing pickings...");

            var domain = new object[][] { 
                new object[] { "state", "in", new string[] { "assigned", "confirmed", "waiting" } },
                new object[] { "picking_type_code", "in", new string[] { "incoming", "outgoing" } }
            };

            var fields = new string[] { "name", "picking_type_code", "id" };
            var pickings = await _odooClient.SearchAndReadAsync<OdooOrderDto>("stock.picking", domain, fields);
            _logger.LogInformation("[POLLING] Odoo returned {Count} pickings", pickings.Count);
            
            foreach (var pick in pickings)
            {
                var moveDomain = new object[][] { new object[] { "picking_id", "=", pick.Id } };
                var moveFields = new string[] { "product_id", "product_uom_qty" };
                
                try {
                    var moves = await _odooClient.SearchAndReadAsync<OdooOrderLineDto>("stock.move", moveDomain, moveFields);
                    pick.Lines = moves;
                    _logger.LogInformation("[POLLING] Handled picking {Ref} with {Count} lines", pick.Name, moves.Count);
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error fetching lines for picking {Ref}", pick.Name);
                }

                await _orderSync.SyncPicking(pick, pick.Picking_Type_Code);
            }
        }
    }
}
