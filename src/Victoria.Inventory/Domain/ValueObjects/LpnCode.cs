using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Victoria.Core;

namespace Victoria.Inventory.Domain.ValueObjects
{
    public sealed class LpnCode : ValueObject
    {
        [JsonProperty]
        public string Value { get; set; }

        [JsonConstructor]
        private LpnCode() { } // Marten

        public LpnCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("LPN code cannot be empty.");
            
            // Relaxed validation: Allow Alphanumeric + dashes, 3-30 chars (Support for PTC, CONT, etc.)
            if (!Regex.IsMatch(value, @"^[A-Z0-9-]{3,30}$"))
                throw new ArgumentException("Invalid LPN format. Expected Alphanumeric (A-Z, 0-9, -) 3-30 chars.");

            Value = value;
        }

        public static LpnCode Create(string value) => new LpnCode(value);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}
