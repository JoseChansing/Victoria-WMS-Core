using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Victoria.Core;

namespace Victoria.Inventory.Domain.ValueObjects
{
    public sealed class LocationCode : ValueObject
    {
        public string Value { get; set; }
        public string Zone { get; set; }
        public string Aisle { get; set; }
        public string Rack { get; set; }
        public string Level { get; set; }
        public string Position { get; set; }

        public LocationCode() { } // Marten

        public LocationCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Location code cannot be empty.");

            // Formato standard: Z01-P02-R01-N1-01 (Zona-Pasillo-Rack-Nivel-Posicion)
            // O ubicaciones especiales: DOCK-LPN, STAGE-RESERVE, etc.
            var match = Regex.Match(value, @"^(Z\d{2})-(P\d{2})-(R\d{2})-(N\d{1,2})-(\d{2})$");
            if (match.Success)
            {
                Value = value;
                Zone = match.Groups[1].Value;
                Aisle = match.Groups[2].Value;
                Rack = match.Groups[3].Value;
                Level = match.Groups[4].Value;
                Position = match.Groups[5].Value;
            }
            else if (value is "DOCK-LPN" or "STAGE-RESERVE" or "STAGE-PICKING" or "PHOTO-STATION") 
            {
                Value = value;
                Zone = value.Split('-')[0]; // DOCK o STAGE
                Aisle = "00";
                Rack = "00";
                Level = "0";
                Position = "00";
            }
            else
            {
                throw new ArgumentException("Invalid Location format. Expected: ZXX-PXX-RXX-NXX-XX or Master Location (DOCK-LPN, etc.)");
            }
        }

        public static LocationCode Create(string value) => new LocationCode(value);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}
