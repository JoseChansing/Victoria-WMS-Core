using System;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class IntegrationState
    {
        public string TenantId { get; set; } = string.Empty;
        public string IntegrationKey { get; set; } = string.Empty; // ej. "ODOO_PRODUCT_SYNC"
        public DateTime LastSyncDate { get; set; }
        public string Metadata { get; set; } = string.Empty;
    }
}
