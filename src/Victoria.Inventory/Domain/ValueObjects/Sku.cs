using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Victoria.Core;

namespace Victoria.Inventory.Domain.ValueObjects
{
    public sealed class Sku : ValueObject
    {
        [JsonProperty]
        public string Value { get; private set; }

        [JsonConstructor]
        private Sku() { } // Marten constructor

        private Sku(string value)
        {
            Value = value?.Trim().ToUpperInvariant() ?? throw new ArgumentNullException(nameof(value));
        }

        public static Sku Create(string value) => new Sku(value);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}
