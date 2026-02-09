-- 03_LayoutBase.sql
-- Configuración de Layout de Almacén Estándar

-- Este script inicializa las ubicaciones para el Tenant principal (PERFECTPTY)
-- En Marten, las tablas se generan por agregado. Aquí simulamos la estructura JSON.

-- 1. Zona de Recepción (Staging)
INSERT INTO mt_doc_location (id, data) VALUES 
('REC-STAGE-01', '{"TenantId": "PERFECTPTY", "Code": "REC-STAGE-01", "Type": "Staging", "Zone": "RECEPCION", "IsActive": true}');

-- 2. Zona de Almacén (Reserve) - Pasillo 01, Rack A, Nivel 1, Posiciones 1-10
INSERT INTO mt_doc_location (id, data) VALUES 
('Z01-P01-RA-N1-01', '{"TenantId": "PERFECTPTY", "Code": "Z01-P01-RA-N1-01", "Type": "Reserve", "Zone": "ALMACEN", "IsActive": true}'),
('Z01-P01-RA-N1-02', '{"TenantId": "PERFECTPTY", "Code": "Z01-P01-RA-N1-02", "Type": "Reserve", "Zone": "ALMACEN", "IsActive": true}'),
('Z01-P01-RA-N1-03', '{"TenantId": "PERFECTPTY", "Code": "Z01-P01-RA-N1-03", "Type": "Reserve", "Zone": "ALMACEN", "IsActive": true}'),
('Z01-P01-RA-N1-04', '{"TenantId": "PERFECTPTY", "Code": "Z01-P01-RA-N1-04", "Type": "Reserve", "Zone": "ALMACEN", "IsActive": true}'),
('Z01-P01-RA-N1-05', '{"TenantId": "PERFECTPTY", "Code": "Z01-P01-RA-N1-05", "Type": "Reserve", "Zone": "ALMACEN", "IsActive": true}');

-- 3. Zona de Despacho (DockDoor)
INSERT INTO mt_doc_location (id, data) VALUES 
('DOCK-01', '{"TenantId": "PERFECTPTY", "Code": "DOCK-01", "Type": "DockDoor", "Zone": "DESPACHO", "IsActive": true}');

SELECT 'Warehouse Layout initialized for PERFECTPTY' as result;
