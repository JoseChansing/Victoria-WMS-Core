# Guía de Ejecución: Despliegue Fase 2 (Hydration & Launch)

Sigue estos comandos secuencialmente en tu terminal SSH conectada a la EC2.

## 1. Preparación del Servidor (EC2 Setup)
```bash
# Actualizar el sistema
sudo dnf update -y

# Instalar Docker
sudo dnf install -y docker
sudo systemctl enable docker
sudo systemctl start docker
sudo usermod -a -G docker ec2-user
# Cierra y vuelve a entrar por SSH para aplicar permisos de docker

# Instalar Git
sudo dnf install -y git

# Instalar Cliente PostgreSQL 15
sudo dnf install -y postgresql15
```

## 2. Hidratación de Base de Datos (RDS Seeding)
*Nota: Reemplaza [CONTRASEÑA] con la clave maestra vicky_password.*

```bash
# Conexión al RDS (Paso interactivo)
# psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d postgres

# Comandos SQL dentro de psql:
# CREATE DATABASE victoria_wms;
# \c victoria_wms

# (Sal de psql con \q y ejecuta los scripts si los tienes en la EC2 o pega su contenido)
# Para ejecutar los scripts generados previamente:
# psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_wms -f 01_Tenants.sql
# psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_wms -f 02_SuperAdmin.sql
# psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_wms -f 03_LayoutBase.sql
```

## 3. Configuración del Entorno (.env)
Crea el archivo `.env` en el directorio raíz de la aplicación:

```bash
cat <<EOF > .env
AWS_ACCOUNT_ID=625981588256
AWS_REGION=us-east-2
RDS_HOSTNAME=victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com
REDIS_HOSTNAME=victoria-cache.qyicld.0001.use2.cache.amazonaws.com
DB_NAME=victoria_wms
DB_USER=vicky_admin
DB_PASSWORD=vicky_password
EOF
```

## 4. Lanzamiento de Aplicación
```bash
# Subir el docker-compose.prod.yml (o crearlo con nano)
# Una vez tengas el archivo en la EC2:
docker compose -f docker-compose.prod.yml up -d
```

## 5. Smoke Test (Prueba de Humo)
```bash
# Verificar contenedores
docker ps

# Ver logs de la API y confirmar conexión Marten/Redis
docker logs victoria-api

# Prueba de salud local
curl http://localhost:8080/health
```
