using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Victoria.Infrastructure.Integration.Odoo;

public class OdooIdCheck
{
    public static async Task Main()
    {
        var client = new OdooRpcClient(
            new HttpClient(), 
            NullLogger<OdooRpcClient>.Instance,
            "https://victoriastock.odoo.com",
            "victoriastock",
            "j.ortega@victoriawms.dev",
            "34204e38e68449c28892d192cc9436ca6d588506"
        );

        Console.WriteLine("--- ODOO MOVE AUDIT (PICKING 7) ---");
        var moves = await client.SearchAndReadAsync<Dictionary<string, object>>("stock.move", 
            new object[][] { new object[] { "picking_id", "=", 7 } }, 
            new string[] { "id", "product_id", "product_uom", "state", "quantity" });

        foreach (var m in moves)
        {
            var pId = (m["product_id"] as object[])?[1];
            Console.WriteLine($"ID: {m["id"]} | Product: {pId} | Qty: {m["quantity"]} | State: {m["state"]}");
        }
    }
}
