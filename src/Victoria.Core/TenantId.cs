using System;
using System.Collections.Generic;

namespace Victoria.Core
{
    public sealed class TenantId : ValueObject
    {
        public string Value { get; }

        private static readonly HashSet<string> ValidTenants = new(StringComparer.OrdinalIgnoreCase)
        {
            "PERFECTPTY", "NATSUKI", "PDM", "FILTROS"
        };

        private TenantId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("TenantId cannot be empty.");

            if (!ValidTenants.Contains(value))
                throw new ArgumentException($"Invalid TenantId: {value}. Allowed values: {string.Join(", ", ValidTenants)}");

            Value = value.ToUpperInvariant();
        }

        public static TenantId Create(string value) => new TenantId(value);

        public static readonly TenantId PerfectPty = new("PERFECTPTY");
        public static readonly TenantId Natsuki = new("NATSUKI");
        public static readonly TenantId Pdm = new("PDM");
        public static readonly TenantId Filtros = new("FILTROS");

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}
