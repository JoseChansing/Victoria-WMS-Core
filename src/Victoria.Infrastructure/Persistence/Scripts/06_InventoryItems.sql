-- 06_InventoryItems.sql
CREATE TABLE IF NOT EXISTS inventoryitems (
    id VARCHAR(50) PRIMARY KEY,
    sku VARCHAR(50) NOT NULL,
    quantity INT DEFAULT 0,
    status VARCHAR(50),
    location VARCHAR(50),
    tenantid VARCHAR(50) NOT NULL,
    lastupdated TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_inventory_tenant ON inventoryitems(tenantid);
CREATE INDEX IF NOT EXISTS idx_inventory_sku ON inventoryitems(sku);
