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
            
            var domain = new object[][] { 
                new object[] { "active", "=", true }, 
                new object[] { "detailed_type", "=", "product" },
                new object[] { "company_id", "=", odooCompanyId }
            };

            var fields = new string[] { 
                "id", "display_name", "default_code", "weight", 
                "image_1920", "image_128", "product_tmpl_id" 
            };

            var products = await _odooClient.SearchAndReadAsync<OdooProductDto>("product.product", domain, fields);
            
            foreach (var p in products)
            {
                p.Company_Id = odooCompanyId;
                await _productSync.SyncProduct(p);
            }
        }

        private async Task SyncOrders()
        {
            _logger.LogInformation("[POLLING] Syncing Ready Pickings (Incoming/Outgoing)");

            var domain = new object[][] { 
                new object[] { "state", "=", "assigned" },
                new object[] { "picking_type_code", "in", new string[] { "incoming", "outgoing" } }
            };

            var fields = new string[] { "name", "picking_type_code", "company_id", "id" };
            var pickings = await _odooClient.SearchAndReadAsync<OdooOrderDto>("stock.picking", domain, fields);
            
            foreach (var pick in pickings)
            {
                await _orderSync.SyncPicking(pick, pick.Picking_Type_Code);
            }
        }
    }
}
