using System;
using System.Collections.Generic;
using Victoria.Core;

namespace Victoria.Inventory.Domain.ValueObjects
{
    public sealed class Sku : ValueObject
    {
        public string Value { get; }

        private Sku(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("SKU cannot be empty.");
            
            if (value.Length < 3)
                throw new ArgumentException("SKU is too short.");

            Value = value;
        }

        public static Sku Create(string value) => new Sku(value);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}
