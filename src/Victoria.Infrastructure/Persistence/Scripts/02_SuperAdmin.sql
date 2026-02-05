-- 02_SuperAdmin.sql
-- Creación del SuperUsuario inicial

-- Nota: El hash de "Victoria2024!" (ejemplo educativo, en producción usar Identity Framework hash)
-- Hash PBKDF2 sugerido: AQAAAAEAACcQAAAAE... (Simulado para este ejemplo)

INSERT INTO mt_doc_user (id, data) VALUES 
('admin@victoriawms.dev', '{
    "Email": "admin@victoriawms.dev",
    "FullName": "Victoria SuperAdmin",
    "Role": "SuperAdmin",
    "PasswordHash": "AQAAAAEAACcQAAAAEBb/X9tV8M0Jb2Z0lB9z7...simulated...",
    "IsActive": true,
    "CreatedAt": "2024-02-01T00:00:00Z"
}')
ON CONFLICT (id) DO UPDATE SET data = EXCLUDED.data;

SELECT 'SuperAdmin created successfully' as result;
