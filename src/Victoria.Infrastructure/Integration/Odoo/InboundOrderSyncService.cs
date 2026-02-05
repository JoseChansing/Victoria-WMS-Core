using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooOrderLineDto
    {
        public string Product_Id { get; set; } = string.Empty;
        public int Product_Uom_Qty { get; set; }
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
            { 1, "PERFECTPTY" }, { 2, "NATSUKI" }, { 3, "PDM" }, { 4, "FILTROS" }
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

            var order = new InboundOrder
            {
                Id = odooPicking.Id.ToString(),
                OrderNumber = odooPicking.Name,
                Supplier = "Odoo Supplier", // Podríamos buscar el partner_id si lo pidiéramos
                Status = "Pending",
                TenantId = tenantId,
                TotalUnits = 0, // Simplificación: las unidades vendrán de las líneas
                Lines = (odooPicking.Lines ?? new()).Select(l => new InboundLine {
                    Sku = l.Product_Id,
                    ExpectedQty = l.Product_Uom_Qty,
                    ReceivedQty = 0
                }).ToList()
            };

            order.TotalUnits = order.Lines.Sum(l => l.ExpectedQty);

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
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
