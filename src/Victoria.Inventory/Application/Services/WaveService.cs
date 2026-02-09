using System;
using System.Collections.Generic;
using System.Linq;
using Marten;
using Microsoft.Extensions.Logging;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.Entities;
using Victoria.Inventory.Domain.ValueObjects;

// Explicit Aliases
using InventoryTask = Victoria.Inventory.Domain.Entities.Task;
using InventoryTaskStatus = Victoria.Inventory.Domain.Entities.TaskStatus;

namespace Victoria.Inventory.Application.Services
{
    public class WaveService
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<WaveService> _logger;

        public WaveService(IDocumentSession session, ILogger<WaveService> logger)
        {
            _session = session;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task<Guid> AllocateWaveAsync(Guid[] orderIds)
        {
            _logger.LogInformation("Allocating wave for {Count} orders", orderIds.Length);

            var orders = await _session.LoadManyAsync<OutboundOrder>(orderIds);
            if (orders.Count == 0) throw new ArgumentException("No orders found");

            var wave = new Wave($"WAVE-{DateTime.UtcNow:yyyyMMddHHmmss}");
            _session.Store(wave);

            var tasksToCreate = new List<InventoryTask>();

            foreach (var order in orders)
            {
                if (!string.IsNullOrEmpty(order.ExtensionWaveId))
                {
                    _logger.LogWarning("Order {OrderId} is already in wave {WaveId}", order.OrderId, order.ExtensionWaveId);
                    continue;
                }

                wave.AddOrder(order.Id);
                order.AssignWave(wave.WaveNumber);
                _session.Update(order);

                foreach (var line in order.Lines)
                {
                    double remainingQty = line.ProductUomQty;

                    // 1. Find Inventory
                    // We need to match by ProductId (Odoo ID) or SKU. 
                    // LPN has SKU. OutboundLine has ProductName/ProductId. Use ProductName as SKU? 
                    // Assuming line.ProductName maps to Lpn.Sku.Value for now.
                    // Ideally we should have Sku in OutboundLine or map ProductId to Sku. 
                    // Using "ProductName" as SKU proxy for this implementation as per context.
                    var sku = line.ProductName; 

                    var lpns = await _session.Query<Lpn>()
                        .Where(l => l.Sku.Value == sku && l.Status == LpnStatus.Putaway)
                        .ToListAsync();

                    // 2. Priority 1: Full LPN Match
                    var fullMatch = lpns.FirstOrDefault(l => l.Quantity == remainingQty && l.AllocatedQuantity == 0);
                    if (fullMatch != null)
                    {
                        fullMatch.Allocate(order.OrderId, Sku.Create(sku), "SYSTEM", "WAVE_ENGINE");
                        tasksToCreate.Add(InventoryTask.CreateFullPalletMove(
                            wave.Id, 
                            fullMatch.CurrentLocationId ?? "UNKNOWN", 
                            remainingQty,
                            sku,
                            fullMatch.Id
                        ));
                        _session.Update(fullMatch);
                        remainingQty = 0;
                    }
                    else
                    {
                        // 3. Priority 2: Loose Pick
                        // Filter LPNs with available quantity
                        var availableLpns = lpns
                            .Where(l => (l.Quantity - l.AllocatedQuantity) > 0)
                            .OrderByDescending(l => l.Quantity - l.AllocatedQuantity) 
                            .ToList();

                        foreach (var lpn in availableLpns)
                        {
                            if (remainingQty <= 0) break;

                            int available = lpn.Quantity - lpn.AllocatedQuantity;
                            int toPick = (int)Math.Min(available, remainingQty); 

                            lpn.ReserveQuantity(toPick);
                            tasksToCreate.Add(InventoryTask.CreatePickToTote(
                                wave.Id,
                                order.Id,
                                line.LineId,
                                lpn.CurrentLocationId ?? "UNKNOWN",
                                "TOTE-NEW", 
                                lpn.Id,
                                sku,
                                toPick
                            ));
                            _session.Update(lpn);
                            remainingQty -= toPick;
                        }
                    }

                    // 4. Shortage -> Cycle Count
                    if (remainingQty > 0)
                    {
                        _logger.LogWarning("Shortage detected for {Sku}. Missing: {Qty}", sku, remainingQty);
                        
                        // Find a location to count. Can be a location where we *thought* we had it (but allocated count was high?) 
                        // or just any previous location.
                        // For now, if we found NO lpns, we pick "UNKNOWN" or ask logic to find "Empty" location history.
                        // Simple approach: Pick the first LPN we examined (even if 0 qty) or "SYSTEM".
                        // Logic Update: If we found LPNs but they were all fully allocated, pick one to count.
                        // If we found NO LPNs, maybe we can't generate a specific location count easily.
                        
                        string locationToCount = lpns.FirstOrDefault()?.CurrentLocationId ?? "UNKNOWN";
                        
                        tasksToCreate.Add(InventoryTask.CreateCycleCount(wave.Id, locationToCount, sku));
                    }
                }
            }

            wave.Allocate();
            _session.Update(wave);
            _session.StoreObjects(tasksToCreate);
            await _session.SaveChangesAsync();

            return wave.Id;
        }
    }
}
