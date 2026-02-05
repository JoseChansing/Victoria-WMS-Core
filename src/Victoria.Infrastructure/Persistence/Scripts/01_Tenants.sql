-- 01_Tenants.sql
-- Inicialización de Compañías / Tenants en Victoria WMS

-- Aseguramos que la tabla exista (Marten usualmente la crea, pero si es manual:)
-- CREATE TABLE IF NOT EXISTS mt_doc_tenant (id varchar PRIMARY KEY, data jsonb);

INSERT INTO mt_doc_tenant (id, data) VALUES 
('PERFECTPTY', '{"Name": "PerfectPTY", "Prefix": "PTC-", "ExternalId": "1"}'),
('PDM',        '{"Name": "PDM",        "Prefix": "PDM-", "ExternalId": "2"}'),
('FILTROS',   '{"Name": "Filtros",    "Prefix": "FLT-", "ExternalId": "3"}'),
('NATSUKI',   '{"Name": "Natsuki",    "Prefix": "NAT-", "ExternalId": "4"}')
ON CONFLICT (id) DO UPDATE SET data = EXCLUDED.data;

SELECT 'Tenants initialized successfully' as result;
