-- 04_InboundOrders.sql
CREATE TABLE IF NOT EXISTS InboundOrders (
    Id VARCHAR PRIMARY KEY,
    OrderNumber VARCHAR NOT NULL,
    Supplier VARCHAR NOT NULL,
    Status VARCHAR NOT NULL,
    Date VARCHAR NOT NULL,
    TotalUnits INTEGER NOT NULL,
    TenantId VARCHAR NOT NULL,
    Data JSONB NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_inbound_tenant ON InboundOrders(TenantId);
