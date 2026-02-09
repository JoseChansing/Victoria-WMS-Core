using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Victoria.Core;

namespace Victoria.Inventory.Domain.ValueObjects
{
    public sealed class LpnCode : ValueObject
    {
        public string Value { get; }

        private LpnCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("LPN code cannot be empty.");
            
            // Example validation: must start with LPN followed by 10 digits
            if (!Regex.IsMatch(value, @"^LPN\d{10}$"))
                throw new ArgumentException("Invalid LPN format. Expected LPN + 10 digits.");

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
