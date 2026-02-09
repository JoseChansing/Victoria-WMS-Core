using System;
using Victoria.Core;

namespace Victoria.Inventory.Domain.Exceptions
{
    public class TenantSecurityException : Exception
    {
        public TenantSecurityException(string message) : base(message) { }
    }
}

namespace Victoria.Inventory.Domain.Security
{
    using Victoria.Inventory.Domain.Aggregates;
    using Victoria.Inventory.Domain.Exceptions;

    public static class TenantGuard
    {
        // LPN Guards removed due to Single-Tenant Architecture

    }
}
