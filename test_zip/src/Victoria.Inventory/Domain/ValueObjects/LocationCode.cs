using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Victoria.Core;

namespace Victoria.Inventory.Domain.ValueObjects
{
    public sealed class LocationCode : ValueObject
    {
        public string Value { get; }
        public string Zone { get; }
        public string Aisle { get; }
        public string Rack { get; }
        public string Level { get; }
        public string Position { get; }

        private LocationCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Location code cannot be empty.");

            // Formato: Z01-P02-R01-N1-01 (Zona-Pasillo-Rack-Nivel-Posicion)
            var match = Regex.Match(value, @"^(Z\d{2})-(P\d{2})-(R\d{2})-(N\d{1,2})-(\d{2})$");
            if (!match.Success)
                throw new ArgumentException("Invalid Location format. Expected: ZXX-PXX-RXX-NXX-XX");

            Value = value;
            Zone = match.Groups[1].Value;
            Aisle = match.Groups[2].Value;
            Rack = match.Groups[3].Value;
            Level = match.Groups[4].Value;
            Position = match.Groups[5].Value;
        }

        public static LocationCode Create(string value) => new LocationCode(value);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}
