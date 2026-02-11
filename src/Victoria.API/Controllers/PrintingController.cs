using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Marten;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Infrastructure.Services;
using Victoria.Inventory.Domain.ValueObjects;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/printing")]
    public class PrintingController : ControllerBase
    {
        private readonly IDocumentSession _session;
        private readonly EpcEncoderService _encoder;

        public PrintingController(IDocumentSession session, EpcEncoderService encoder)
        {
            _session = session;
            _encoder = encoder;
        }

        [HttpPost("rfid/batch")]
        public async Task<IActionResult> GenerateRfidBatchZpl([FromBody] BatchPrintRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
                return BadRequest("No IDs provided");

            var lpns = await _session.Query<Lpn>().Where(x => x.Id.In(request.Ids)).ToListAsync();
            
            // Load products for categories
            var skuCodes = lpns.Select(x => x.Sku?.Value).Where(x => x != null).Distinct().ToArray();
            var products = await _session.Query<Product>().Where(x => x.Id.In(skuCodes!)).ToListAsync();
            
            // Industrial ZPL Config
            string zplConfig = "^CI28^PW812^LL609"; // Unicode + 4x3 inches at 203dpi
            var zplBuilder = new System.Text.StringBuilder();

            foreach (var lpnId in request.Ids)
            {
                var lpn = lpns.FirstOrDefault(x => x.Id == lpnId);
                if (lpn == null) continue;

                string sku = lpn.Sku?.Value ?? "UNKNOWN";
                var product = products.FirstOrDefault(p => p.Id == lpn.Sku?.Value);
                string category = product?.Category ?? "";
                
                // Extract numeric serial from LPN ID
                if (!long.TryParse(System.Text.RegularExpressions.Regex.Match(lpn.Id, @"\d+").Value, out long serial))
                {
                    serial = (long)(DateTime.UtcNow - new DateTime(2020, 1, 1)).TotalSeconds;
                }

                string epcHex = _encoder.EncodeSgtin96(sku, serial);
                
                // STEP 1: STRICT EPC PADDING (ZD621R requirements)
                // Ensure exactly 24 characters. Pad left if short, truncate if long.
                epcHex = epcHex.PadLeft(24, '0').Substring(0, 24);
                
                // Server-side Debug Log
                Console.WriteLine($"[RFID] Intentando escribir EPC (24 chars): {epcHex} para LPN: {lpn.Id}");

                // Category line (only if category exists)
                string categoryLine = !string.IsNullOrEmpty(category) ? $"^FO60,300^A0N,25,25^FDCAT: {category}^FS\r\n" : "";

                // STEP 2: ZD621R OPTIMIZED SEQUENCE (Desktop Logic)
                // ^RS8,,,3 -> Retry 3 times if chip is not found.
                zplBuilder.Append($@"^XA{zplConfig}
^RS8,,,3
^RFW,H,1,12,1,^FD{epcHex}^FS
^FO60,60^A0N,30,30^FDPERFECTPTY - LPN^FS
^FO60,110^A0N,60,60^FDSKU: {sku}^FS
^FO60,190^A0N,40,40^FDLPN: {lpn.Id}^FS
{categoryLine}^FO60,250^BCN,100,Y,N,N^FD{lpn.Id}^FS
^XZ
");
            }

            return Content(zplBuilder.ToString(), "text/plain");
        }

        [HttpPost("lpn/{lpnId}/rfid")]
        public async Task<IActionResult> GenerateRfidZpl(string lpnId)
        {
            var lpn = await _session.LoadAsync<Lpn>(lpnId);
            if (lpn == null) return NotFound("LPN not found");

            // Relaxed check: If SKU is null, usage "UNKNOWN"
            string skuValue = lpn.Sku?.Value ?? "UNKNOWN";
            
            Product product = null;
            string category = "";
            if (lpn.Sku != null) 
            {
                 product = await _session.LoadAsync<Product>(lpn.Sku.Value);
                 category = product?.Category ?? "";
            }

            // Extract numeric GTIN/EAN from Sku or Ean (if SKU is not numeric, we try to parse it anyway as a string)
            string gtin = skuValue;
            
            // LPN Id as numeric serial
            if (!long.TryParse(System.Text.RegularExpressions.Regex.Match(lpnId, @"\d+").Value, out long serial))
            {
                serial = (long)(DateTime.UtcNow - new DateTime(2020, 1, 1)).TotalSeconds;
            }

            string epcHex;
            try
            {
                // Validate if GTIN is numeric
                if (!long.TryParse(gtin, out _))
                {
                    // Fallback for non-numeric SKUs: Generate a safe, dummy EPC Hex directly
                    // This mimics a valid SGTIN-96 header (30 ...) but ensures we don't crash the encoder
                    epcHex = "30000000000000000000" + serial.ToString("X").PadLeft(4, '0');
                    if (epcHex.Length > 24) epcHex = epcHex.Substring(0, 24); // SAFETY CLIP
                    epcHex = epcHex.PadRight(24, '0');
                }
                else
                {
                    epcHex = _encoder.EncodeSgtin96(gtin, serial);
                }
            }
            catch
            {
                // Absolute fail-safe: valid 24-char hex string
                epcHex = "300000000000000000000000"; 
            }

            try
            {
                // Sanitization to prevent breaking characters like double quotes in ZPL or JSON
                Func<string, string> sanitize = (s) => s?.Replace("\"", "").Replace("\\", "/").Trim() ?? "";

                string safeSku = sanitize(skuValue);
                string safeLpn = sanitize(lpn.Id);
                string receipt = sanitize(lpn.SelectedOrderId ?? "N/A");
                string safeCategory = sanitize(category);
                string timestamp = lpn.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                // Calculate volume in m3
                double volume = (lpn.PhysicalAttributes.Length * lpn.PhysicalAttributes.Width * lpn.PhysicalAttributes.Height) / 1000000.0;

                // ZD621R Optimized Sequence (Desktop)
                string epc24 = epcHex.PadLeft(24, '0').Substring(0, 24);
                
                string categoryLine = !string.IsNullOrEmpty(safeCategory) ? $"^FO60,300^A0N,25,25^FDCAT: {safeCategory}^FS\r\n" : "";
                
                string zpl = $@"
^XA
^CI28
^PW812
^LL609
^RS8,,,3
^RFW,H,1,12,1,^FD{epc24}^FS
^FO60,60^A0N,30,30^FDPERFECTPTY - LPN^FS
^FO60,100^A0N,60,60^FDSKU: {safeSku}^FS
^FO60,180^A0N,40,40^FDLPN: {safeLpn}^FS
^FO60,240^A0N,25,25^FDRECEIPT: {receipt}^FS
^FO60,270^A0N,25,25^FDDATE: {timestamp}^FS
{categoryLine}^FO60,340^BY3^BCN,100,Y,N,N^FD{safeLpn}^FS
^XZ";

                return Ok(new
                {
                    LpnId = lpn.Id,
                    EpcHex = epc24,
                    Zpl = zpl.Trim()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = "Failed to encode EPC", Details = ex.Message });
            }
        }

        [HttpGet("lpn/{lpnId}/label")]
        public async Task<IActionResult> GetLabelHtml(string lpnId)
        {
            var lpn = await _session.LoadAsync<Lpn>(lpnId);
            if (lpn == null) return NotFound("LPN not found");

            string sku = lpn.Sku?.Value ?? "UNKNOWN";
            
            // Fetch the InboundOrder to get the OrderNumber
            string receipt = "N/A";
            if (!string.IsNullOrEmpty(lpn.SelectedOrderId))
            {
                var order = await _session.LoadAsync<InboundOrder>(lpn.SelectedOrderId);
                receipt = order?.OrderNumber ?? lpn.SelectedOrderId;
            }
            
            // Fetch Product to get Category
            string category = "";
            if (lpn.Sku != null)
            {
                var product = await _session.LoadAsync<Product>(lpn.Sku.Value);
                category = product?.Category ?? "";
            }
            
            string timestamp = lpn.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            double volume = (lpn.PhysicalAttributes.Length * lpn.PhysicalAttributes.Width * lpn.PhysicalAttributes.Height) / 1000000.0;

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <script src=""https://cdn.jsdelivr.net/npm/jsbarcode@3.11.5/dist/JsBarcode.all.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ margin: 0; padding: 0; font-family: Arial, Helvetica, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; background: #f0f0f0; }}
        .label-container {{
            width: 4in; height: 3in; background: white; border: 2px solid black; box-sizing: border-box;
            padding: 8px 10px; display: flex; flex-direction: column;
        }}
        .top-header {{ font-size: 6px; font-weight: bold; color: #333; margin-bottom: 2px; }}
        .receipt-title {{ font-size: 13px; font-weight: 900; text-align: center; border-bottom: 2px solid black; padding-bottom: 3px; margin-bottom: 4px; }}
        
        .barcode-section {{ margin-bottom: 3px; }}
        .barcode-label {{ font-size: 6px; font-weight: bold; margin-bottom: 1px; }}
        .barcode-full {{ width: 100%; height: 38px; }}
        .barcode-half {{ width: 45%; height: 28px; }}
        .barcode-value {{ font-size: 7px; font-weight: bold; text-align: center; margin-top: 1px; margin-bottom: 2px; }}
        
        .product-row {{ display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1px; }}
        .product-left {{ flex: 1; }}
        .product-right {{ text-align: right; }}
        .item-label {{ font-size: 8px; font-weight: bold; margin-bottom: 1px; }}
        .brand-info {{ font-size: 8px; font-weight: bold; color: #333; }}
        .sku-large {{ font-size: 38px; font-weight: 900; line-height: 1; letter-spacing: -1px; }}
        
        .divider {{ border-top: 1px dashed #999; margin: 4px 0; }}
        
        .stats-row {{ display: flex; justify-content: space-between; font-size: 8px; margin-bottom: 2px; }}
        .stats-row .label {{ font-weight: bold; }}
        .stats-row .value {{ font-weight: 900; }}
        
        .footer {{ font-size: 6px; color: #555; margin-top: auto; padding-top: 2px; }}
        
        @media print {{
            * {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
            body {{ margin: 0 !important; padding: 0 !important; background: white; }}
            @page {{ size: 4in 3in; margin: 0; }}
            .label-container {{ margin: 0; border: 2px solid black; }}
        }}
    </style>
</head>
<body>
    <div class=""label-container"">
        <div class=""top-header"">VICTORIA WMS CORE</div>
        <div class=""receipt-title"">RECEIPT: {receipt}</div>
        
        <div class=""barcode-section"">
            <div class=""barcode-label"">LPN BARCODE</div>
            <svg class=""barcode-full"" id=""barcode-lpn""></svg>
            <div class=""barcode-value"">{lpn.Id}</div>
        </div>
        
        <div class=""product-row"">
            <div class=""product-left"">
                <div class=""item-label"">ITEM:</div>
                <div class=""brand-info"">BRAND: {lpn.Brand}{(!string.IsNullOrEmpty(lpn.Sides) ? $" | SIDE: {lpn.Sides}" : "")}</div>
                {(!string.IsNullOrEmpty(category) ? $@"<div class=""brand-info"">CAT: {category}</div>" : "")}
            </div>
            <div class=""product-right"">
                <div class=""sku-large"">{sku}</div>
            </div>
        </div>
        
        {(!string.IsNullOrEmpty(lpn.ProductBarcode) ? $@"
        <div class=""barcode-section"">
            <div class=""barcode-label"">PRODUCT BARCODE</div>
            <svg class=""barcode-half"" id=""barcode-prod""></svg>
        </div>" : "")}
        
        <div class=""divider""></div>
        
        <div class=""stats-row"">
            <span class=""label"">QTY:</span>
            <span class=""value"">{lpn.Quantity} UN</span>
        </div>
        <div class=""stats-row"">
            <span class=""label"">WEIGHT:</span>
            <span class=""value"">{lpn.PhysicalAttributes.Weight} KG</span>
        </div>
        <div class=""stats-row"">
            <span class=""label"">DIM:</span>
            <span class=""value"">{lpn.PhysicalAttributes.Length}x{lpn.PhysicalAttributes.Width}x{lpn.PhysicalAttributes.Height} CM</span>
        </div>
        <div class=""stats-row"">
            <span class=""label"">VOL:</span>
            <span class=""value"">{volume:F4} M3</span>
        </div>
        
        <div class=""footer"">{timestamp}</div>
    </div>
    <script>
        JsBarcode(""#barcode-lpn"", ""{lpn.Id}"", {{ format: ""CODE128"", displayValue: false, margin: 0, height: 35 }});
        {(!string.IsNullOrEmpty(lpn.ProductBarcode) ? $@"JsBarcode(""#barcode-prod"", ""{lpn.ProductBarcode}"", {{ format: ""CODE128"", displayValue: false, margin: 0, height: 25 }});" : "")}
        window.onload = function() {{ window.print(); }}
    </script>
</body>
</html>";

            return Content(html, "text/html");
        }

        [HttpGet("batch")]
        public async Task<IActionResult> GetBatchLabels([FromQuery] string ids)
        {
            if (string.IsNullOrEmpty(ids)) return BadRequest("No IDs provided");
            var lpnIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries);
            return await GenerateBatchHtml(lpnIds);
        }

        [HttpPost("batch")]
        public async Task<IActionResult> PostBatchLabels([FromBody] BatchPrintRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any()) 
                return BadRequest("No IDs provided in body");
            
            return await GenerateBatchHtml(request.Ids.ToArray());
        }

        private async Task<IActionResult> GenerateBatchHtml(string[] lpnIds)
        {
            var lpns = await _session.Query<Lpn>().Where(x => x.Id.In(lpnIds)).ToListAsync();
            
            // Fetch associated Orders and Products for better data
            var orderIds = lpns.Select(x => x.SelectedOrderId).Where(x => x != null).Distinct().ToArray();
            var skuCodes = lpns.Select(x => x.Sku?.Value).Where(x => x != null).Distinct().ToArray();
            
            var orders = await _session.Query<InboundOrder>().Where(x => x.Id.In(orderIds!)).ToListAsync();
            var products = await _session.Query<Product>().Where(x => x.Id.In(skuCodes!)).ToListAsync();

            var htmlBuilder = new System.Text.StringBuilder();
            htmlBuilder.Append(@"<!DOCTYPE html><html><head>
                <script src=""https://cdn.jsdelivr.net/npm/jsbarcode@3.11.5/dist/JsBarcode.all.min.js""></script>
                <style>
                body { margin:0; padding:0; font-family: Arial, Helvetica, sans-serif; }
                .label-container { 
                    width: 4in; height: 3in; 
                    border: 2px solid black; 
                    page-break-after: always; 
                    box-sizing: border-box;
                    padding: 8px 10px;
                    display: flex;
                    flex-direction: column;
                }
                .top-header { font-size: 6px; font-weight: bold; color: #333; margin-bottom: 2px; }
                .receipt-title { font-size: 13px; font-weight: 900; text-align: center; border-bottom: 2px solid black; padding-bottom: 3px; margin-bottom: 4px; }
                
                .barcode-section { margin-bottom: 3px; }
                .barcode-label { font-size: 6px; font-weight: bold; margin-bottom: 1px; }
                .barcode-full { width: 100%; height: 38px; }
                .barcode-half { width: 45%; height: 28px; }
                .barcode-value { font-size: 7px; font-weight: bold; text-align: center; margin-top: 1px; margin-bottom: 2px; }
                
                .product-row { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1px; }
                .product-left { flex: 1; }
                .product-right { text-align: right; }
                .item-label { font-size: 8px; font-weight: bold; margin-bottom: 1px; }
                .brand-info { font-size: 8px; font-weight: bold; color: #333; }
                .sku-large { font-size: 38px; font-weight: 900; line-height: 1; letter-spacing: -1px; }
                
                .divider { border-top: 1px dashed #999; margin: 4px 0; }
                
                .stats-row { display: flex; justify-content: space-between; font-size: 8px; margin-bottom: 2px; }
                .stats-row .label { font-weight: bold; }
                .stats-row .value { font-weight: 900; }
                
                .footer { font-size: 6px; color: #555; margin-top: auto; padding-top: 2px; }
                
                @media print {
                    * { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
                    body { margin: 0 !important; padding: 0 !important; }
                    @page { size: 4in 3in; margin: 0; }
                    .label-container { margin: 0; page-break-after: always; }
                }
            </style></head><body>");

            foreach (var lpnId in lpnIds)
            {
                var lpn = lpns.FirstOrDefault(x => x.Id == lpnId);
                if (lpn == null) continue;

                var order = orders.FirstOrDefault(o => o.Id == lpn.SelectedOrderId);
                var product = products.FirstOrDefault(p => p.Id == lpn.Sku?.Value);
                
                string sku = lpn.Sku?.Value ?? "UNKNOWN";
                string receipt = order?.OrderNumber ?? lpn.SelectedOrderId ?? "N/A";
                string category = product?.Category ?? "";
                string timestamp = lpn.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                double volume = (lpn.PhysicalAttributes.Length * lpn.PhysicalAttributes.Width * lpn.PhysicalAttributes.Height) / 1000000.0;

                htmlBuilder.Append($@"
                <div class=""label-container"">
                    <div class=""top-header"">VICTORIA WMS CORE</div>
                    <div class=""receipt-title"">RECEIPT: {receipt}</div>
                    
                    <div class=""barcode-section"">
                        <div class=""barcode-label"">LPN BARCODE</div>
                        <svg class=""barcode-full"" id=""barcode-lpn-{lpn.Id}""></svg>
                        <div class=""barcode-value"">{lpn.Id}</div>
                    </div>
                    
                    <div class=""product-row"">
                        <div class=""product-left"">
                            <div class=""item-label"">ITEM:</div>
                            <div class=""brand-info"">BRAND: {lpn.Brand}{(!string.IsNullOrEmpty(lpn.Sides) ? $" | SIDE: {lpn.Sides}" : "")}</div>
                            {(!string.IsNullOrEmpty(category) ? $@"<div class=""brand-info"">CAT: {category}</div>" : "")}
                        </div>
                        <div class=""product-right"">
                            <div class=""sku-large"">{sku}</div>
                        </div>
                    </div>
                    
                    {(!string.IsNullOrEmpty(lpn.ProductBarcode) ? $@"
                    <div class=""barcode-section"">
                        <div class=""barcode-label"">PRODUCT BARCODE</div>
                        <svg class=""barcode-half"" id=""barcode-prod-{lpn.Id}""></svg>
                    </div>" : "")}
                    
                    <div class=""divider""></div>
                    
                    <div class=""stats-row"">
                        <span class=""label"">QTY:</span>
                        <span class=""value"">{lpn.Quantity} UN</span>
                    </div>
                    <div class=""stats-row"">
                        <span class=""label"">WEIGHT:</span>
                        <span class=""value"">{lpn.PhysicalAttributes.Weight} KG</span>
                    </div>
                    <div class=""stats-row"">
                        <span class=""label"">DIM:</span>
                        <span class=""value"">{lpn.PhysicalAttributes.Length}x{lpn.PhysicalAttributes.Width}x{lpn.PhysicalAttributes.Height} CM</span>
                    </div>
                    <div class=""stats-row"">
                        <span class=""label"">VOL:</span>
                        <span class=""value"">{volume:F4} M3</span>
                    </div>
                    
                    <div class=""footer"">{timestamp}</div>
                </div>
                <script>
                    JsBarcode(""#barcode-lpn-{lpn.Id}"", ""{lpn.Id}"", {{ format: ""CODE128"", displayValue: false, margin: 0, height: 35 }});
                    {(!string.IsNullOrEmpty(lpn.ProductBarcode) ? $@"JsBarcode(""#barcode-prod-{lpn.Id}"", ""{lpn.ProductBarcode}"", {{ format: ""CODE128"", displayValue: false, margin: 0, height: 25 }});" : "")}
                </script>");
            }

            htmlBuilder.Append("<script>window.onload = function() { window.print(); }</script></body></html>");
            return Content(htmlBuilder.ToString(), "text/html");
        }
    }

    public class BatchPrintRequest
    {
        public List<string> Ids { get; set; } = new();
    }
}
