using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace Victoria.Infrastructure.Services
{
    public class EpcEncoderService
    {
        private readonly string _companyPrefix;
        private readonly int _filterValue;

        public EpcEncoderService(string companyPrefix, int filterValue = 1)
        {
            _companyPrefix = companyPrefix;
            _filterValue = filterValue;
        }

        public string EncodeSgtin96(string sku, long lpnId)
        {
            // STEP 1: DETERMINISTIC HASHING FOR ALPHANUMERIC SKUS (Anti-Collision)
            // Using a custom sum/mult hash (like FNV-1a inspiration) to ensure SKU-A != SKU-B
            long skuHash = 0;
            if (!string.IsNullOrEmpty(sku))
            {
                foreach (char c in sku.ToUpperInvariant())
                {
                    skuHash = (skuHash * 31) + c;
                }
            }
            
            // Map to 14 bits (0-16383)
            long itemReference = Math.Abs(skuHash) % 16384; 

            // Config: Use numeric prefix or fallback
            string cpDigits = System.Text.RegularExpressions.Regex.Replace(_companyPrefix ?? "770", "[^0-9]", "");
            if (string.IsNullOrEmpty(cpDigits)) cpDigits = "770";
            long companyPrefixNum = long.Parse(cpDigits);

            bool[] bits = new bool[96];

            // 1. Header (8 bits): 0x30 (SGTIN-96)
            WriteBits(bits, 0, 8, 0x30);

            // 2. Filter (3 bits): 001 (Store/Retail)
            WriteBits(bits, 8, 3, _filterValue);

            // 3. Partition (3 bits): 101 (5) -> 30 bits CP / 14 bits Item
            WriteBits(bits, 11, 3, 5);

            // 4. Company Prefix (30 bits)
            WriteBits(bits, 14, 30, companyPrefixNum);

            // 5. Item Reference (14 bits)
            WriteBits(bits, 44, 14, itemReference);

            // 6. Serial (38 bits)
            WriteBits(bits, 58, 38, lpnId);

            return BitsToHex(bits);
        }

        private void WriteBits(bool[] bits, int start, int length, long value)
        {
            for (int i = 0; i < length; i++)
            {
                bits[start + length - 1 - i] = ((value >> i) & 1) == 1;
            }
        }

        private string BitsToHex(bool[] bits)
        {
            StringBuilder hex = new StringBuilder(24);
            for (int i = 0; i < 96; i += 4)
            {
                int val = 0;
                if (bits[i]) val += 8;
                if (bits[i + 1]) val += 4;
                if (bits[i + 2]) val += 2;
                if (bits[i + 3]) val += 1;
                hex.Append(val.ToString("X"));
            }
            return hex.ToString();
        }

        private (int Partition, int CpBits, int IrBits) GetPartitionConfig(int cpDigits)
        {
            return cpDigits switch
            {
                12 => (0, 40, 4),
                11 => (1, 37, 7),
                10 => (2, 34, 10),
                9 => (3, 30, 14),
                8 => (4, 27, 17),
                7 => (5, 24, 20),
                6 => (6, 20, 24),
                _ => throw new ArgumentException($"Invalid GS1 Company Prefix length: {cpDigits}")
            };
        }
    }
}
