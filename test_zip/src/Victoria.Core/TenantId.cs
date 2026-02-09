using System;
using System.Collections.Generic;

namespace Victoria.Core
{
    public sealed class TenantId : ValueObject
    {
        public string Value { get; }

        private TenantId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("TenantId cannot be empty.");

            Value = value.ToUpperInvariant();
        }

        public static TenantId Create(string value) => new TenantId(value);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}
