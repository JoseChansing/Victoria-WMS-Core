using Marten;
using System;
using System.Linq;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Aggregates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Victoria.Diagnostics
{
    public class OrderStatusCheck
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddMarten(opts =>
                    {
                        opts.Connection("Host=localhost;Port=5432;Database=victoria_wms;Username=vicky_admin;Password=vicky_password");
                        opts.AutoCreateSchemaObjects = Weasel.Core.AutoCreate.All;
                    });
                })
                .Build();

            using (var scope = host.Services.CreateScope())
            {
                var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
                string orderNumber = "COLON/IN/00734";
                
                var order = await session.Query<InboundOrder>()
                    .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

                if (order == null)
                {
                    Console.WriteLine($"[ERROR] Order {orderNumber} not found in database.");
                }
                else
                {
                    Console.WriteLine($"[INFO] Order Found: {order.OrderNumber}");
                    Console.WriteLine($"[INFO] Status: {order.Status}");
                    Console.WriteLine($"[INFO] IsCrossdock: {order.IsCrossdock}");
                    Console.WriteLine($"[INFO] OdooId: {order.Id}");
                    Console.WriteLine($"[INFO] Date: {order.Date}");
                }
            }
        }
    }
}
