$ErrorActionPreference = "Stop"

Write-Host "INICIANDO DESPLIEGUE A PRODUCCION (Remoto/Limpio)" -ForegroundColor Cyan

# Variables
$EC2_HOST = "ec2-user@3.14.182.244"
$KEY_PATH = ".\victoria-key-fixed.pem"

# 1. Frontend Build
Write-Host "1. Frontend Build..." -ForegroundColor Yellow
Set-Location "src/Victoria.UI"
$env:VITE_API_URL = "https://api.victoriawms.dev/api/v1"
npm run build
if ($LASTEXITCODE -ne 0) { Write-Error "Build Failed"; exit 1 }

Write-Host "Subiendo S3..." -ForegroundColor Yellow
aws s3 sync dist/ s3://app.victoriawms.dev --delete --region us-east-2
aws cloudfront create-invalidation --distribution-id E1IAC81MX1E6UM --paths "/*"
Set-Location ../..

# 2. Backend Package (Incluyendo cambios locales NO confirmados)
Write-Host "2. Backend Package..." -ForegroundColor Yellow
if (Test-Path deploy-package.zip) { Remove-Item deploy-package.zip }
Compress-Archive -Path "src/Victoria.API", "src/Victoria.Core", "src/Victoria.Infrastructure", "src/Victoria.Inventory", "docker-compose.prod.yml", ".env.production" -DestinationPath deploy-package.zip

Write-Host "Subiendo archivos a EC2..." -ForegroundColor Yellow
$DestPackage = $EC2_HOST + ":~/deploy-package.zip"
$DestSql04 = $EC2_HOST + ":~/04_InboundOrders.sql"
$DestSql05 = $EC2_HOST + ":~/05_Products.sql"
$DestSql06 = $EC2_HOST + ":~/06_InventoryItems.sql"

scp -i $KEY_PATH -o StrictHostKeyChecking=no deploy-package.zip $DestPackage
scp -i $KEY_PATH -o StrictHostKeyChecking=no src/Victoria.Infrastructure/Persistence/Scripts/04_InboundOrders.sql $DestSql04
scp -i $KEY_PATH -o StrictHostKeyChecking=no src/Victoria.Infrastructure/Persistence/Scripts/05_Products.sql $DestSql05
scp -i $KEY_PATH -o StrictHostKeyChecking=no src/Victoria.Infrastructure/Persistence/Scripts/06_InventoryItems.sql $DestSql06

# 3. Remote Execution
Write-Host "3. Remote Execution..." -ForegroundColor Yellow

$RemoteScript = @"
set -e
echo '>>> Unzipping...'
mkdir -p ~/victoria-wms
unzip -o ~/deploy-package.zip -d ~/victoria-wms
cd ~/victoria-wms

echo '>>> Configurando .env...'
if [ -f .env.production ]; then
    cp .env.production .env
else
    echo "WARNING: .env.production not found"
fi

echo '>>> Limpiando contenedores previos para evitar conflictos...'
docker rm -f api-perfect worker-perfect victoria-api victoria-worker nginx-proxy || true

echo '>>> Creando base de datos si no existe...'
docker run --rm \
  -e PGPASSWORD=vicky_password \
  postgres:15-alpine \
  psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d postgres -c "CREATE DATABASE victoria_perfect;" || true

echo '>>> Rebuild Docker (Construyendo en servidor)...'
docker compose -f docker-compose.prod.yml up -d --build

echo '>>> DB Migration (via Docker)...'
sleep 30
docker run --rm \
  -v /home/ec2-user/04_InboundOrders.sql:/tmp/04.sql \
  -v /home/ec2-user/05_Products.sql:/tmp/05.sql \
  -v /home/ec2-user/06_InventoryItems.sql:/tmp/06.sql \
  -e PGPASSWORD=vicky_password \
  postgres:15-alpine \
  sh -c 'psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_perfect -f /tmp/04.sql && psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_perfect -f /tmp/05.sql && psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_perfect -f /tmp/06.sql'

# 5. Verificación Final (Audit & Logs)
echo '>>> Esperando inicialización de servicios (30s)...'
sleep 30

echo '>>> [AUDITORIA] Tablas en la base de datos (Buscando mt_doc_...):'
docker run --rm \
  -e PGPASSWORD=vicky_password \
  postgres:15-alpine \
  sh -c 'psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_perfect -c "\dt mt_doc_*"'
  
docker run --rm \
  -e PGPASSWORD=vicky_password \
  postgres:15-alpine \
  sh -c 'psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_perfect -c "SELECT count(*) FROM mt_doc_inboundorder;" || echo "Tabla mt_doc_inboundorder aun no creada."'

echo '>>> [LOGS] Victoria Worker (Marten status):'
docker compose logs --tail 50 worker-perfect

echo '>>> Finalizado.'
"@

# Limpiar CRLF
$RemoteScript = $RemoteScript -replace "`r", ""
$ScriptBytes = [System.Text.Encoding]::UTF8.GetBytes($RemoteScript)
$Base64Script = [System.Convert]::ToBase64String($ScriptBytes)

ssh -i $KEY_PATH -o StrictHostKeyChecking=no $EC2_HOST "echo $Base64Script | base64 -d | bash"

Write-Host "DESPLIEGUE FINALIZADO" -ForegroundColor Green
