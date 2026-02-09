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
        public static void EnsureSameTenant(TenantId actorTenant, Lpn lpn)
        {
            if (actorTenant != lpn.Tenant)
            {
                throw new TenantSecurityException($"Access Denied: Tenant '{actorTenant}' cannot access LPN '{lpn.Id}' belonging to Tenant '{lpn.Tenant}'.");
            }
        }

        public static void EnsureSameTenant(TenantId actorTenant, Location location)
        {
            if (actorTenant != location.Tenant)
            {
                throw new TenantSecurityException($"Access Denied: Tenant '{actorTenant}' cannot access Location '{location.Code}' belonging to Tenant '{location.Tenant}'.");
            }
        }

        public static void EnsureCompatibility(Lpn lpn, Location location)
        {
            if (lpn.Tenant != location.Tenant)
            {
                throw new TenantSecurityException($"Inventory Contamination Risk: Cannot place LPN '{lpn.Id}' (Tenant: {lpn.Tenant}) in Location '{location.Code}' (Tenant: {location.Tenant}).");
            }
        }
    }
}
