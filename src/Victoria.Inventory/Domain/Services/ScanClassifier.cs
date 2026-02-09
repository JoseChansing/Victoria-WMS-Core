using System.Text.RegularExpressions;

namespace Victoria.Inventory.Domain.Services
{
    public enum ScanType
    {
        Sku,
        Lpn,
        Rfid
    }

    public interface IScanClassifier
    {
        ScanType Classify(string input);
    }

    public class ScanClassifier : IScanClassifier
    {
        // 96-bit hexadecimal string (24 characters)
        private static readonly Regex RfidRegex = new Regex(@"^[0-9A-Fa-f]{24}$", RegexOptions.Compiled);
        
        // PTC prefix followed by 16 numeric digits
        private static readonly Regex LpnRegex = new Regex(@"^PTC\d{16}$", RegexOptions.Compiled);

        public ScanType Classify(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return ScanType.Sku;

            if (RfidRegex.IsMatch(input))
                return ScanType.Rfid;

            if (LpnRegex.IsMatch(input))
                return ScanType.Lpn;

            return ScanType.Sku;
        }
    }
}
