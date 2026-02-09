-- 05_Products.sql
CREATE TABLE IF NOT EXISTS Products (
    Id VARCHAR PRIMARY KEY,
    Sku VARCHAR NOT NULL,
    Name VARCHAR NOT NULL,
    TenantId VARCHAR NOT NULL,
    OdooId INTEGER,
    Data JSONB NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_products_tenant ON Products(TenantId);
CREATE INDEX IF NOT EXISTS idx_products_sku ON Products(Sku);
