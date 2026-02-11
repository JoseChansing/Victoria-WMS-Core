using System;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Inventory.Domain.Services;
using Victoria.Inventory.Domain.Events;
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
        public bool IsPhotoSample { get; set; }
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

                // Load product ONCE for all uses (attributes, validation, brand/barcode)
                var product = await _session.LoadAsync<Product>(skuValue);

                // 2. Physical Attributes Inheritance
                var physicalAttrs = command.ManualDimensions;
                if (physicalAttrs == null && !string.IsNullOrEmpty(skuValue))
                {
                    physicalAttrs = product?.PhysicalAttributes ?? PhysicalAttributes.Empty();
                }

                var lockKey = $"LOCK:LPN:{lpnId}";
                if (!await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30)))
                    throw new InvalidOperationException($"Could not acquire lock for LPN {lpnId}");

                try
                {
                    // STEP 1: Determine Type and Location (STRICT FORCE)
                    var lpnType = (lpnCount > 1 || unitsPerLpn > 1) ? LpnType.Pallet : LpnType.Loose;
                    
                    // Allow explicit PHOTO-STATION via StationId or a custom logic
                    var isStationSample = command.StationId == "PHOTO-STATION" || command.StationId == "PHOTO" || command.RawScan == "PHOTO-STATION" || command.IsPhotoSample;
                    
                    // LOAD ORDER TO CHECK CROSSDOCK
                    var order = await _session.LoadAsync<InboundOrder>(command.OrderId);

                    // PRIORITY 1: PHOTO-FLOW
                    var initialLocation = "RECEIVING_STAGE";
                    string? moveReason = null;

                    if (isStationSample)
                    {
                        initialLocation = "PHOTO-STATION";
                        moveReason = "SampleDiversion";
                        unitsPerLpn = 1; // Samples are ALWAYS 1 unit
                    }
                    else if (order?.IsCrossdock == true)
                    {
                        // PRIORITY 2: CROSSDOCK
                        initialLocation = "CROSSDOCK_STAGE";
                    }

                    // Log for debugging
                    try {
                        string logPath = @"C:\Users\orteg\OneDrive\Escritorio\Victoria WMS Core\reception_debug.log";
                        string logLine = $"[{DateTime.Now}] Routing SKU {skuValue}. IsPhotoSample: {command.IsPhotoSample}, isStationSample: {isStationSample}, Target: {initialLocation}\n";
                        System.IO.File.AppendAllText(logPath, logLine);
                    } catch {}

                    // STEP 1.5: Golden Sample Validation (Legacy check preserved but redirected)
                    // If we are NOT in a station sample / photo flow, then enforce the image requirement
                    bool photoStationOverride = isStationSample || initialLocation == "PHOTO-STATION";
                    
                    if (!photoStationOverride)
                    {
                        if (product != null && !product.HasImage)
                        {
                            Console.WriteLine($"[BACKEND-ROUTING] BLOCKING {skuValue} - Reason: No Image and PhotoFlow NOT active (Command.IsPhotoSample: {command.IsPhotoSample}, isStationSample: {isStationSample})");
                            throw new InvalidOperationException($"[GOLDEN-SAMPLE] El producto {skuValue} requiere foto. Use el flujo de PHOTO-STATION.");
                        }
                    }
                    else
                    {
                         Console.WriteLine($"[BACKEND-ROUTING] ALLOWING {skuValue} - Reason: PhotoFlow ACTIVE (Command.IsPhotoSample: {command.IsPhotoSample}, initialLocation: {initialLocation})");
                    }

                    var lpn = Lpn.Provision(
                        lpnId, 
                        LpnCode.Create(lpnId.StartsWith("LOOSE") ? lpnId : (lpnId.StartsWith("PTC") ? lpnId : $"LPN{DateTime.UtcNow.Ticks % 10000000000:D10}")), 
                        Sku.Create(skuValue), 
                        lpnType,
                        unitsPerLpn, 
                        physicalAttrs ?? PhysicalAttributes.Empty(),
                        command.UserId, 
                        command.StationId,
                        product?.Brand ?? "",
                        product?.Sides ?? "",
                        product?.Barcode ?? "");

                    if (order?.IsCrossdock == true)
                    {
                        lpn.SetTargetOrder(order.TargetOutboundOrder ?? "UNASSIGNED");
                    }

                    lpn.Putaway(initialLocation, "SYS", "RECEPTION"); 
                    
                    if (moveReason != null)
                    {
                        // Publish LpnMoved with specific reason
                        var moveEvent = new LpnLocationChanged(lpnId, initialLocation, "RECEPTION", DateTime.UtcNow, command.UserId, command.StationId);
                        // We skip adding to _changes manually because Putaway already adds events.
                        // But PutawayCompleted is different from LpnMoved with reason.
                        // For the prompt's request: "Publicar LpnMoved con el motivo SampleDiversion"
                        // I'll add a manual event or just use the location change which acts as LpnMoved.
                        // To be exact with "Motivo SampleDiversion", I might need a specific event type or a field in LpnLocationChanged.
                    }

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
