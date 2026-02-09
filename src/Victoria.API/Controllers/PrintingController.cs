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
            // Industrial ZPL Config
            string zplConfig = "^CI28^PW812^LL609"; // Unicode + 4x3 inches at 203dpi
            var zplBuilder = new System.Text.StringBuilder();

            foreach (var lpnId in request.Ids)
            {
                var lpn = lpns.FirstOrDefault(x => x.Id == lpnId);
                if (lpn == null) continue;

                string sku = lpn.Sku?.Value ?? "UNKNOWN";
                
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

                // STEP 2: ZD621R OPTIMIZED SEQUENCE (Desktop Logic)
                // ^RS8,,,3 -> Retry 3 times if chip is not found.
                zplBuilder.Append($@"^XA{zplConfig}
^RS8,,,3
^RFW,H,1,12,1,^FD{epcHex}^FS
^FO60,60^A0N,30,30^FDPERFECTPTY - LPN^FS
^FO60,110^A0N,60,60^FDSKU: {sku}^FS
^FO60,190^A0N,40,40^FDLPN: {lpn.Id}^FS
^FO60,250^BCN,100,Y,N,N^FD{lpn.Id}^FS
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
            if (lpn.Sku != null) 
            {
                 product = await _session.LoadAsync<Product>(lpn.Sku.Value);
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
                string timestamp = lpn.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                // Calculate volume in m3
                double volume = (lpn.PhysicalAttributes.Length * lpn.PhysicalAttributes.Width * lpn.PhysicalAttributes.Height) / 1000000.0;

                // ZD621R Optimized Sequence (Desktop)
                string epc24 = epcHex.PadLeft(24, '0').Substring(0, 24);
                
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
^FO60,240^A0N,25,25^FDRECIBO: {receipt}^FS
^FO60,270^A0N,25,25^FDFECHA: {timestamp}^FS
^FO60,300^A0N,25,25^FDVOL: {volume:F4} m3^FS
^FO60,340^BY3^BCN,100,Y,N,N^FD{safeLpn}^FS
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
            string receipt = lpn.SelectedOrderId ?? "N/A";
            string timestamp = lpn.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <script src=""https://cdn.jsdelivr.net/npm/jsbarcode@3.11.5/dist/JsBarcode.all.min.js""></script>
    <style>
        body {{ margin: 0; padding: 0; font-family: Arial, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; background: #f0f0f0; }}
        .label-container {{
            width: 4in; height: 3in; background: white; border: 1px solid black; box-sizing: border-box;
            padding: 15px; display: flex; flex-direction: column; align-items: center; justify-content: space-between;
        }}
        .header {{ font-size: 16px; font-weight: bold; width: 100%; text-align: center; border-bottom: 1px solid #000; padding-bottom: 5px; }}
        .barcode-container {{ flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; width: 100%; }}
        .barcode-svg {{ width: 90%; height: 100px; }}
        .sku-text {{ font-size: 24px; font-weight: 900; margin-top: 5px; }}
        .footer {{ 
            width: 100%; display: flex; justify-content: space-between; align-items: flex-end;
            font-size: 10px; font-weight: bold; border-top: 1px solid #000; padding-top: 5px;
        }}
        .id-ref {{ font-size: 8px; color: #666; }}
    </style>
</head>
<body>
    <div class=""label-container"">
        <div class=""header"">RECIBO: {receipt}</div>
        <div class=""barcode-container"">
            <svg class=""barcode-svg"" id=""barcode""></svg>
            <div class=""sku-text"">{sku}</div>
        </div>
        <div class=""footer"">
            <span>FECHA: {timestamp}</span>
            <span class=""id-ref"">ID: {lpn.Id}</span>
        </div>
    </div>
    <script>
        JsBarcode(""#barcode"", ""{sku}"", {{ format: ""CODE128"", displayValue: false, margin: 0 }});
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
                body { margin:0; padding:0; font-family: Arial, sans-serif; }
                .label-container { 
                    width: 4in; height: 3in; 
                    border: 1px solid black; 
                    page-break-after: always; 
                    box-sizing: border-box;
                    padding: 15px;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    justify-content: space-between;
                }
                .header { font-size: 16px; font-weight: bold; width: 100%; text-align: center; border-bottom: 1px solid #000; padding-bottom: 5px; }
                .barcode-container { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; width: 100%; }
                .barcode-svg { width: 90%; height: 100px; }
                .sku-text { font-size: 24px; font-weight: 900; margin-top: 5px; }
                .footer { 
                    width: 100%; display: flex; justify-content: space-between; align-items: flex-end;
                    font-size: 10px; font-weight: bold; border-top: 1px solid #000; padding-top: 5px;
                }
                .id-ref { font-size: 8px; color: #666; }
            </style></head><body>");

            foreach (var lpnId in lpnIds)
            {
                var lpn = lpns.FirstOrDefault(x => x.Id == lpnId);
                if (lpn == null) continue;

                var order = orders.FirstOrDefault(o => o.Id == lpn.SelectedOrderId);
                string sku = lpn.Sku?.Value ?? "UNKNOWN";
                string receipt = order?.OrderNumber ?? lpn.SelectedOrderId ?? "N/A";
                string timestamp = lpn.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

                htmlBuilder.Append($@"
                <div class=""label-container"">
                    <div class=""header"">RECIBO: {receipt}</div>
                    <div class=""barcode-container"">
                        <svg class=""barcode-svg"" id=""barcode-{lpn.Id}""></svg>
                        <div class=""sku-text"">{sku}</div>
                    </div>
                    <div class=""footer"">
                        <span>FECHA: {timestamp}</span>
                        <span class=""id-ref"">ID: {lpn.Id}</span>
                    </div>
                </div>
                <script>
                    JsBarcode(""#barcode-{lpn.Id}"", ""{sku}"", {{ format: ""CODE128"", displayValue: false, margin: 0 }});
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
