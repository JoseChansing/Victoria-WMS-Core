using System;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Services;
using Marten;

namespace Victoria.Inventory.Application.Commands
{
    public class ReceiveLpnCommand
    {
        public string TenantId { get; set; } = string.Empty;
        public string? LpnId { get; set; }
        public string? RawScan { get; set; }
        public string? Sku { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public int ExpectedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public int LpnCount { get; set; } = 1; // Bulk support
        public int UnitsPerLpn { get; set; } // Bulk support
        public bool IsUnitMode { get; set; } // UNIT vs BULK
        public string UserId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public PhysicalAttributes? ManualDimensions { get; set; }
    }

    public class ReceiveLpnHandler
    {
        private readonly Victoria.Core.Infrastructure.IEventStore _eventStore;
        private readonly Victoria.Core.Infrastructure.ILockService _lockService;
        private readonly IScanClassifier _classifier;
        private readonly IEpcParser _epcParser;
        private readonly ILpnFactory _lpnFactory;
        private readonly Marten.IDocumentSession _session;

        public ReceiveLpnHandler(
            Victoria.Core.Infrastructure.IEventStore eventStore, 
            Victoria.Core.Infrastructure.ILockService lockService,
            IScanClassifier classifier,
            IEpcParser epcParser,
            ILpnFactory lpnFactory,
            Marten.IDocumentSession session)
        {
            _eventStore = eventStore;
            _lockService = lockService;
            _classifier = classifier;
            _epcParser = epcParser;
            _lpnFactory = lpnFactory;
            _session = session;
        }

        public async Task<List<string>> Handle(ReceiveLpnCommand command)
        {
            var generatedIds = new List<string>();
            var lpnCount = command.LpnCount > 0 ? command.LpnCount : 1;
            var unitsPerLpn = command.UnitsPerLpn > 0 ? command.UnitsPerLpn : command.ReceivedQuantity;

            for (int i = 0; i < lpnCount; i++)
            {
                string lpnId = (i == 0 && !string.IsNullOrEmpty(command.LpnId)) ? command.LpnId : string.Empty;
                string skuValue = command.Sku ?? string.Empty;
                
                // 1. Logic for polymorphic input (only for the first one if looping, or all if same scan)
                if (!string.IsNullOrEmpty(command.RawScan))
                {
                    var scanType = _classifier.Classify(command.RawScan);
                    switch (scanType)
                    {
                        case ScanType.Rfid:
                            var epc = _epcParser.Parse(command.RawScan);
                            skuValue = epc.Sku;
                            lpnId = string.IsNullOrEmpty(lpnId) ? await _lpnFactory.CreateNextAsync() : lpnId;
                            break;
                        case ScanType.Lpn:
                            lpnId = command.RawScan;
                            break;
                        case ScanType.Sku:
                            skuValue = command.RawScan;
                            lpnId = string.IsNullOrEmpty(lpnId) ? await _lpnFactory.CreateNextAsync() : lpnId;
                            break;
                    }
                }

                if (string.IsNullOrEmpty(lpnId))
                {
                    lpnId = await _lpnFactory.CreateNextAsync();
                }

                // Fallback: If SKU is still empty but we have a RawScan, assume RawScan IS the SKU (if not used for LPN)
                if (string.IsNullOrEmpty(skuValue) && !string.IsNullOrEmpty(command.RawScan) && command.RawScan != lpnId)
                {
                    skuValue = command.RawScan;
                }
                
                // Final Check: If SKU is still empty/null, use "UNKNOWN" to avoid crashing Create
                if (string.IsNullOrEmpty(skuValue)) skuValue = "UNKNOWN";

                // 2. Physical Attributes Inheritance
                var physicalAttrs = command.ManualDimensions;
                if (physicalAttrs == null && !string.IsNullOrEmpty(skuValue))
                {
                    var product = await _session.LoadAsync<Product>(skuValue);
                    physicalAttrs = product?.PhysicalAttributes ?? PhysicalAttributes.Empty();
                }

                var lockKey = $"LOCK:LPN:{lpnId}";
                if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30)))
                    throw new InvalidOperationException($"Could not acquire lock for LPN {lpnId}");

                try
                {
                    // STEP 1: Determine Type and Location (STRICT FORCE)
                    // If Standard/Loose mode is used (IsUnitMode), we IGNORE quantity/loops and force Loose
                    var lpnType = command.IsUnitMode ? LpnType.Loose : ((lpnCount > 1 || unitsPerLpn > 1) ? LpnType.Pallet : LpnType.Loose);
                    
                    // Allow explicit PHOTO-STATION via StationId or a custom logic
                    var isStationSample = command.StationId == "PHOTO-STATION" || command.StationId == "PHOTO" || command.RawScan == "PHOTO-STATION";
                    var initialLocation = isStationSample ? "PHOTO-STATION" : (lpnType == LpnType.Pallet ? "DOCK-LPN" : "DOCK-UNITS");

                    // STEP 1.5: Golden Sample Validation (Photo Requirement)
                    if (!isStationSample && initialLocation != "PHOTO-STATION")
                    {
                        var product = await _session.LoadAsync<Product>(skuValue);
                        if (product != null && !product.HasImage)
                        {
                            var order = await _session.LoadAsync<InboundOrder>(command.OrderId);
                            if (order != null)
                            {
                                var line = order.Lines.FirstOrDefault(l => l.Sku == skuValue);
                                if (line != null)
                                {
                                    // Rule: If no image, must leave at least 1 unit for PHOTO-STATION
                                    if (line.ReceivedQty + unitsPerLpn >= line.ExpectedQty)
                                    {
                                        throw new InvalidOperationException($"[GOLDEN-SAMPLE] El producto {skuValue} no tiene imagen en Odoo. DEBE recibir al menos 1 unidad en PHOTO-STATION antes de completar la línea.");
                                    }
                                }
                            }
                        }
                    }

                    // STEP 2: Consolidation Logic (Bucket Pattern)
                    if (lpnType == LpnType.Loose)
                    {
                        skuValue = skuValue.Trim().ToUpperInvariant();
                        // FIX: Use Order-Specific Bucket to avoid merging with Staged items from previous orders
                        lpnId = $"LOOSE-{command.OrderId}-{skuValue}"; 
                        
                        var looseLpn = await _session.LoadAsync<Lpn>(lpnId); // Fast direct load by ID
                        
                        if (looseLpn != null)
                        {
                            looseLpn.AddQuantity(unitsPerLpn, command.UserId, command.StationId);
                            await _eventStore.AppendEventsAsync(looseLpn.Id, -1, looseLpn.Changes);
                            _session.Store(looseLpn);
                            await _session.SaveChangesAsync();
                            generatedIds.Add(looseLpn.Id);
                            continue; // Skip creation, we updated the bucket
                        }
                    }

                    var lpn = Lpn.Provision(
                        lpnId, 
                        LpnCode.Create(lpnId.StartsWith("LOOSE") ? lpnId : (lpnId.StartsWith("PTC") ? lpnId : $"LPN{DateTime.UtcNow.Ticks % 10000000000:D10}")), 
                        Sku.Create(skuValue), 
                        lpnType,
                        unitsPerLpn, 
                        physicalAttrs ?? PhysicalAttributes.Empty(),
                        command.UserId, 
                        command.StationId);

                    lpn.Putaway(initialLocation, "SYS", "RECEPTION"); // Set initial dock location
                    
                    if (unitsPerLpn > command.ExpectedQuantity && lpnCount == 1) // Logic for single LPN overage
                    {
                        lpn.Quarantine($"OVERAGE_PENDING_APPROVAL", command.UserId, command.StationId);
                    }
                    
                    lpn.Receive(command.OrderId, command.UserId, command.StationId);
                    await _eventStore.AppendEventsAsync(lpnId, -1, lpn.Changes);
                    
                    // CRITICAL FIX: Persist the Read Model (Marten) so it can be queried immediately
                    _session.Store(lpn);
                    await _session.SaveChangesAsync();

                    generatedIds.Add(lpnId);
                }
                finally
                {
                    await _lockService.ReleaseLockAsync(lockKey);
                }
            }

            return generatedIds;
        }
    }
}
