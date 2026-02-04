using System;
using System.Collections;
using System.Text;
using Victoria.Inventory.Domain.ValueObjects;

namespace Victoria.Inventory.Domain.Services
{
    public interface IEpcParser
    {
        EpcCode Parse(string hex);
    }

    public class EpcParser : IEpcParser
    {
        public EpcCode Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 24)
                throw new ArgumentException("Invalid EPC Hex length. Expected 96-bit (24 hex chars).");

            byte[] bytes = HexToBytes(hex);
            BitArray bits = new BitArray(bytes);
            
            // Note: BitArray index 0 is the least significant bit of the first byte in some implementations, 
            // but we need standard network bit order (big endian). 
            // Let's reverse for processing if needed or use shifting.
            
            // SGTIN-96 Structure:
            // Header: 8 bits (Index 0-7) -> 0x30 for SGTIN-96
            // Filter: 3 bits (8-10)
            // Partition: 3 bits (11-13)
            // Company Prefix / Item Ref: Variable (based on partition)
            // Serial: 38 bits (last bits)

            ulong bitValue = GetValue(bytes, 0, 8);
            if (bitValue != 0x30)
                 throw new ArgumentException("Unsupported EPC Header. Only SGTIN-96 (0x30) is supported.");

            int partition = (int)GetValue(bytes, 11, 3);
            int companyPrefixBits = GetCompanyPrefixBits(partition);
            int itemRefBits = GetItemRefBits(partition);

            ulong companyPrefix = GetValue(bytes, 14, companyPrefixBits);
            ulong itemRef = GetValue(bytes, 14 + companyPrefixBits, itemRefBits);
            ulong serial = GetValue(bytes, 58, 38);

            string sku = $"{companyPrefix}.{itemRef}";
            return EpcCode.Create(hex, sku, serial.ToString());
        }

        private int GetCompanyPrefixBits(int partition) => partition switch
        {
            0 => 40, 1 => 37, 2 => 34, 3 => 30, 4 => 27, 5 => 24, 6 => 20,
            _ => throw new ArgumentException("Invalid Partition")
        };

        private int GetItemRefBits(int partition) => partition switch
        {
            0 => 4, 1 => 7, 2 => 10, 3 => 14, 4 => 17, 5 => 20, 6 => 24,
            _ => throw new ArgumentException("Invalid Partition")
        };

        private byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        private ulong GetValue(byte[] data, int startBit, int bitCount)
        {
            ulong result = 0;
            for (int i = 0; i < bitCount; i++)
            {
                int currentBit = startBit + i;
                int byteIndex = currentBit / 8;
                int bitOffset = 7 - (currentBit % 8);
                
                if ((data[byteIndex] & (1 << bitOffset)) != 0)
                {
                    result |= (1UL << (bitCount - 1 - i));
                }
            }
            return result;
        }
    }
}
