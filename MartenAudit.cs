using System;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Victoria.Inventory.Domain.Aggregates;

public class MartenDump
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddMarten(opts => {
            opts.Connection("Host=localhost;Database=victoria_wms;Username=postgres;Password=postgres");
        });
        
        using var host = builder.Build();
        var store = host.Services.GetRequiredService<IDocumentStore>();
        using var session = store.LightweightSession();

        var order = await session.Query<InboundOrder>()
            .Where(x => x.OrderNumber == "WH/IN/00007")
            .FirstOrDefaultAsync();

        if (order == null) {
            Console.WriteLine("Order not found!");
            return;
        }

        Console.WriteLine($"Order: {order.OrderNumber} (ID: {order.Id})");
        foreach (var line in order.Lines) {
            Console.WriteLine($"SKU: {line.Sku} | MoveID: {line.OdooMoveId} | Expected: {line.ExpectedQty} | Received: {line.ReceivedQty}");
        }
    }
}
