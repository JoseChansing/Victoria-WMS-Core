$ErrorActionPreference = "Stop"

Write-Host "INICIANDO DESPLIEGUE A PRODUCCION (Base64 Safe Mode)" -ForegroundColor Cyan

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

# 2. Backend Package
Write-Host "2. Backend Package..." -ForegroundColor Yellow
git archive --format=zip HEAD -o deploy-package.zip

Write-Host "Subiendo archivos a EC2..." -ForegroundColor Yellow
$DestPackage = $EC2_HOST + ":~/deploy-package.zip"
$DestSql04 = $EC2_HOST + ":~/04_InboundOrders.sql"
$DestSql05 = $EC2_HOST + ":~/05_Products.sql"

scp -i $KEY_PATH -o StrictHostKeyChecking=no deploy-package.zip $DestPackage
scp -i $KEY_PATH -o StrictHostKeyChecking=no src/Victoria.Infrastructure/Persistence/Scripts/04_InboundOrders.sql $DestSql04
scp -i $KEY_PATH -o StrictHostKeyChecking=no src/Victoria.Infrastructure/Persistence/Scripts/05_Products.sql $DestSql05

# 3. Remote Execution (Base64 Encoded to prevent CRLF issues)
Write-Host "3. Remote Execution..." -ForegroundColor Yellow

# Script bash que se ejecutará en remoto.
# Usamos `docker exec` para correr psql dentro del contenedor 'db' ya que el host no tiene psql.
$RemoteScript = @"
set -e
echo '>>> Unzipping...'
unzip -o deploy-package.zip -d victoria-wms
cd victoria-wms

echo '>>> Rebuild Docker...'
# Usamos docker compose standard (v2)
docker compose -f docker-compose.prod.yml down
docker compose -f docker-compose.prod.yml up -d --build

echo '>>> DB Migration (via Docker)...'
sleep 15
# Copiamos scripts al contenedor primero o usamos input redirection
# Asumimos que el servicio de db se llama 'db' o 'victoria-db' en el compose. Revisando logs anteriores, parece ser parte del stack.
# Usamos el nombre del contenedor generado o el servicio.
# Vamos a enviar el contenido del archivo via stdin a docker exec.

# Usamos un contenedor efimero de postgres para ejecutar los scripts contra RDS
# Montamos los archivos del host al contenedor
docker run --rm \
  -v /home/ec2-user/04_InboundOrders.sql:/tmp/04.sql \
  -v /home/ec2-user/05_Products.sql:/tmp/05.sql \
  -e PGPASSWORD=vicky_password \
  postgres:15-alpine \
  sh -c 'psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_wms -f /tmp/04.sql && psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_wms -f /tmp/05.sql'

echo '>>> Cleanup...'
rm ~/deploy-package.zip
"@

# IMPORTANTE: Reemplazar CRLF (Windows) por LF (Linux) antes de codificar
$RemoteScript = $RemoteScript -replace "`r", ""

# Codificar a Base64 para evitar problemas de parsing en SSH
$ScriptBytes = [System.Text.Encoding]::UTF8.GetBytes($RemoteScript)
$Base64Script = [System.Convert]::ToBase64String($ScriptBytes)

# Ejecutar decodificando en el destino
ssh -i $KEY_PATH -o StrictHostKeyChecking=no $EC2_HOST "echo $Base64Script | base64 -d | bash"

Write-Host "DESPLIEGUE EXITOSO" -ForegroundColor Green
