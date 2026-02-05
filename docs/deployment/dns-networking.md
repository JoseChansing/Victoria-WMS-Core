# Configuración DNS & Networking: Victoria WMS

## 1. Route 53 (o Proveedor DNS Externo)

| Subdominio | Tipo | Valor | Propósito |
| :--- | :--- | :--- | :--- |
| `api.victoriawms.dev` | `A` | `IP_PÚBLICA_EC2` | Punto de entrada para microservicios y PDAs. |
| `app.victoriawms.dev` | `CNAME` | `id.cloudfront.net` | Interfaz web de usuario (Torre de Control). |

## 2. Certificados SSL

### Backend (Certbot en EC2)
Ejecutar el siguiente comando una vez configurado el DNS y el Security Group (Permitir puerto 80):
```bash
sudo certbot --nginx -d api.victoriawms.dev
```

### Frontend (ACM en AWS)
- Solicitar certificado en la región `us-east-1` (Requisito de CloudFront).
- Validar mediante registros DNS.
- Asociar a la distribución de CloudFront.

## 3. Seguridad Adicional
- **WAF (Opcional)**: Habilitar en CloudFront para proteger el frontend contra ataques comunes (SQLi, XSS).
- **HSTS**: Habilitar en Nginx para forzar conexiones seguras.
