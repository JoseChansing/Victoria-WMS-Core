using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Microsoft.Extensions.Logging;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.Entities;
using Victoria.Core.Interfaces;

namespace Victoria.Inventory.Application.Services
{
    public class OutboundOrderSyncService
    {
        private readonly IOdooRpcClient _odoo;
        private readonly IDocumentSession _session;
        private readonly ILogger<OutboundOrderSyncService> _logger;

        public OutboundOrderSyncService(IOdooRpcClient odoo, IDocumentSession session, ILogger<OutboundOrderSyncService> logger)
        {
            _odoo = odoo;
            _session = session;
            _logger = logger;
        }

        public async Task<int> SyncOrdersAsync()
        {
            _logger.LogInformation("Starting Outbound Order Sync...");

            // 1. Search Logic: stock.picking where outgoing and ready
            var domain = new object[][]
            {
                new object[] { "picking_type_code", "=", "outgoing" },
                new object[] { "state", "in", new string[] { "assigned", "confirmed" } } // Assigned = Ready, Confirmed = Waiting
            };

            var fields = new string[] { "id", "name", "partner_id", "scheduled_date", "priority", "move_ids" };

            var pickings = await _odoo.SearchAndReadAsync<OdooPickingDto>("stock.picking", domain, fields);
            
            if (!pickings.Any())
            {
                _logger.LogInformation("No outbound orders found in Odoo.");
                return 0;
            }

            int syncedCount = 0;

            foreach (var pick in pickings)
            {
                // Check if exists
                var existing = await _session.Query<OutboundOrder>().FirstOrDefaultAsync(o => o.OdooId == pick.Id);
                if (existing != null) 
                {
                    // Optionally update status or checks
                    continue; 
                }

                var order = new OutboundOrder(
                    pick.Name, 
                    pick.Id, 
                    ParsePartner(pick.PartnerId), 
                    pick.Priority, 
                    pick.ScheduledDate
                );

                // 2. Fetch Lines (stock.move)
                if (pick.MoveIds != null && pick.MoveIds.Length > 0)
                {
                    var moveFields = new string[] { "id", "product_id", "product_uom_qty", "product_uom", "name" };
                    var moveDomain = new object[][]
                    {
                        new object[] { "id", "in", pick.MoveIds }
                    };

                    var moves = await _odoo.SearchAndReadAsync<OdooMoveDto>("stock.move", moveDomain, moveFields);
                    
                    foreach (var move in moves)
                    {
                        var (prodId, prodName) = ParseTuple(move.ProductId);
                        var (uomId, uomName) = ParseTuple(move.ProductUom);

                        var line = new OutboundLine(
                            Guid.NewGuid().ToString(),
                            move.Id,
                            prodId,
                            prodName,
                            move.ProductUomQty,
                            uomName
                        );
                        
                        order.AddLine(line);
                    }
                }

                _session.Store(order);
                syncedCount++;
            }

            await _session.SaveChangesAsync();
            _logger.LogInformation("Synced {Count} outbound orders.", syncedCount);
            return syncedCount;
        }

        private string ParsePartner(object partnerField)
        {
            if (partnerField is object[] arr && arr.Length > 1) 
                return arr[1]?.ToString() ?? "Unknown";
            return "Unknown";
        }

        private (int, string) ParseTuple(object field)
        {
            if (field is object[] arr && arr.Length > 1)
            {
                int.TryParse(arr[0]?.ToString(), out int id);
                return (id, arr[1]?.ToString() ?? "");
            }
            return (0, "");
        }
        
        // DTOs for Odoo serialization
        public class OdooPickingDto
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public object? PartnerId { get; set; } // [id, name]
            public string? ScheduledDate { get; set; }
            public string? Priority { get; set; }
            [Newtonsoft.Json.JsonProperty("move_ids")]
            public int[]? MoveIds { get; set; } 

        }

        public class OdooMoveDto
        {
            public int Id { get; set; }
            public object? ProductId { get; set; } // [id, name]
            public double ProductUomQty { get; set; }
            public object? ProductUom { get; set; } // [id, name]
            public string? Name { get; set; }
        }
    }
}
