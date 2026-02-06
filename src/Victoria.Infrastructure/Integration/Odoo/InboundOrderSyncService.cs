using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooOrderLineDto
    {
        public int Product_Id { get; set; }
        public double Product_Uom_Qty { get; set; }
    }

    public class OdooOrderDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // OrderNumber
        public int Company_Id { get; set; }
        public string Picking_Type_Code { get; set; } = string.Empty;
        public List<OdooOrderLineDto> Lines { get; set; } = new();
    }

    public class InboundOrderSyncService
    {
        private readonly string _connectionString;
        private readonly ILogger<InboundOrderSyncService> _logger;
        private static readonly Dictionary<int, string> TenantMapping = new()
        {
            { 1, "PERFECTPTY" }
        };

        public InboundOrderSyncService(ILogger<InboundOrderSyncService> logger, IConfiguration config)
        {
            _logger = logger;
            _connectionString = config["POSTGRES_CONNECTION"] ?? "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";
        }

        public async Task SyncPicking(OdooOrderDto odooPicking, string type)
        {
            if (!TenantMapping.TryGetValue(odooPicking.Company_Id, out var tenantId))
                return;

            _logger?.LogInformation("[OdooSync] Persisting {Type} Picking: {Ref} for {Tenant}", type, odooPicking.Name, tenantId);

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var lines = new List<InboundLine>();
            foreach (var l in (odooPicking.Lines ?? new()))
            {
                var line = new InboundLine
                {
                    ExpectedQty = (int)l.Product_Uom_Qty,
                    ReceivedQty = 0
                };

                // BUSCAR SKU Y METADATOS EN DB LOCAL
                var sqlProd = "SELECT Sku, Name, Data->>'ImageSource' as ImageSource FROM Products WHERE OdooId = @odooId AND TenantId = @tenant LIMIT 1";
                using var cmdProd = new NpgsqlCommand(sqlProd, conn);
                cmdProd.Parameters.AddWithValue("odooId", l.Product_Id);
                cmdProd.Parameters.AddWithValue("tenant", tenantId);
                
                using (var readerProd = await cmdProd.ExecuteReaderAsync())
                {
                    if (await readerProd.ReadAsync())
                    {
                        line.Sku = readerProd.GetString(0);
                        line.ProductName = readerProd.GetString(1);
                        line.ImageSource = readerProd.IsDBNull(2) ? null : readerProd.GetString(2);
                    }
                    else
                    {
                        line.Sku = $"ODOO-{l.Product_Id}";
                        _logger.LogWarning("[OdooSync] Product with OdooId {Id} not found in DB. Using fallback SKU.", l.Product_Id);
                    }
                }
                lines.Add(line);
            }

            var order = new InboundOrder
            {
                Id = odooPicking.Id.ToString(),
                OrderNumber = odooPicking.Name,
                Supplier = "Odoo Supplier",
                Status = "Pending",
                TenantId = tenantId,
                Lines = lines,
                TotalUnits = lines.Sum(l => l.ExpectedQty)
            };

            var json = JsonSerializer.Serialize(order);
            var sql = @"
                INSERT INTO InboundOrders (Id, OrderNumber, Supplier, Status, Date, TotalUnits, TenantId, Data)
                VALUES (@id, @num, @sup, @st, @dt, @units, @tenant, @data::jsonb)
                ON CONFLICT (Id) DO UPDATE SET 
                    Data = EXCLUDED.Data,
                    Status = EXCLUDED.Status,
                    TotalUnits = EXCLUDED.TotalUnits;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", order.Id);
            cmd.Parameters.AddWithValue("num", order.OrderNumber);
            cmd.Parameters.AddWithValue("sup", order.Supplier);
            cmd.Parameters.AddWithValue("st", order.Status);
            cmd.Parameters.AddWithValue("dt", order.Date);
            cmd.Parameters.AddWithValue("units", order.TotalUnits);
            cmd.Parameters.AddWithValue("tenant", order.TenantId);
            cmd.Parameters.AddWithValue("data", json);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
