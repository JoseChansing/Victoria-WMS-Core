# Guía de Aprovisionamiento AWS (Victoria WMS - v1.0)

Esta guía detalla los pasos para desplegar Victoria WMS en AWS maximizando el uso de la **Capa Gratuita (Free Tier)**.

## 1. Red (VPC & Seguridad)
### Paso 1: VPC Wizard
- Seleccionar **"VPC with Public and Private Subnets"**.
- Esto permite que tu EC2 sea pública pero tu Base de Datos (RDS) y Redis sean privadas.

### Paso 2: Security Groups
1.  **`victoria-web-sg`**:
    - Inbound: HTTP (80), HTTPS (443), SSH (22).
2.  **`victoria-internal-sg`**:
    - Inbound: Postgres (5432), Redis (6379). **Origen**: Solo el ID del Security Group `victoria-web-sg`.

---

## 2. Bases de Datos (Persistence & Cache)
### Paso 3: RDS PostgreSQL
- **Versión**: 15+.
- **Instance**: `db.t3.micro` (Free Tier).
- **Public Access**: No.
- **Multi-AZ**: Deshabilitado (Crucial para no generar cargos).
- **Security Group**: `victoria-internal-sg`.

### Paso 4: ElastiCache Redis
- **Instance**: `cache.t2.micro` (Free Tier).
- **Clustering**: Deshabilitado.
- **Security Group**: `victoria-internal-sg`.

---

## 3. Servidor de Aplicaciones (Compute)
### Paso 5: EC2 Instance
- **AMI**: Amazon Linux 2023.
- **Instance**: `t3.micro` (Free Tier).
- **Storage**: 20GB gp3 (Capa gratuita permite hasta 30GB).
- **IP**: Asignar una **Elastic IP** (Nota: AWS cobra si la IP no está asociada a una instancia encendida).

---

## 4. Frontend (Storage & CDN)
### Paso 6: S3 Bucket
- Crear bucket `victoria-wms-frontend`.
- Deshabilitar "Block all public access" si se usará como Static Website (o mejor, usar OAI con CloudFront).

### Paso 7: CloudFront
- **Origin**: El bucket S3 anterior.
- **SSL**: Usar un certificado de AWS Certificate Manager (ACM) para tu dominio `victoriawms.dev`.

---

## ⚠️ Advertencias de Costos (Watchlist)
1. **Transferencia de Datos**: La capa gratuita incluye 15GB de salida. Cuidado con logs pesados.
2. **Elastic IP**: Asegúrate de liberarla si apagas la instancia definitivamente.
3. **Snapshots RDS**: Los backups automáticos consumen espacio de almacenamiento. El Free Tier incluye 20GB totales de storage compartido entre DB y Snapshots.
