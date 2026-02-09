using System;

namespace Victoria.Inventory.Domain.Services
{
    public class LabelService
    {
        public string GenerateShippingLabelZpl(string lpnId, string orderId, string destination)
        {
            // Zebra Programming Language (ZPL) simple
            return $@"
^XA
^CF0,60
^FO50,50^FDVICTORIA WMS^FS
^CF0,30
^FO50,130^FDLPN: {lpnId}^FS
^FO50,170^FDORDER: {orderId}^FS
^FO50,210^FDDEST: {destination}^FS
^FO50,260^BY3
^BCN,100,Y,N,N
^FD{lpnId}^FS
^XZ";
        }
    }
}
