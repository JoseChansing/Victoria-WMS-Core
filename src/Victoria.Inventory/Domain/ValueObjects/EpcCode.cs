using System.Collections.Generic;
using Victoria.Core;

namespace Victoria.Inventory.Domain.ValueObjects
{
    public class EpcCode : ValueObject
    {
        public string RawHex { get; }
        public string Sku { get; }
        public string Serial { get; }

        private EpcCode(string rawHex, string sku, string serial)
        {
            RawHex = rawHex.ToUpper();
            Sku = sku;
            Serial = serial;
        }

        public static EpcCode Create(string rawHex, string sku, string serial)
        {
            return new EpcCode(rawHex, sku, serial);
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return RawHex;
        }

        public override string ToString() => $"EPC:{RawHex} (SKU:{Sku}, SN:{Serial})";
    }
}
