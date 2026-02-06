$ErrorActionPreference = "Stop"

Write-Host "INICIANDO DESPLIEGUE A PRODUCCION (ROBUSTO - PERFECTPTY)" -ForegroundColor Cyan

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

# 2. Backend Package (Área de Staging limpia con Robocopy)
Write-Host "2. Backend Package..." -ForegroundColor Yellow
if (Test-Path deploy-package.zip) { Remove-Item deploy-package.zip }
if (Test-Path deploy_pkg) { Remove-Item deploy_pkg -Recurse -Force }

New-Item -ItemType Directory -Path "deploy_pkg/src" | Out-Null
robocopy src/Victoria.API deploy_pkg/src/Victoria.API /S /XD bin obj | Out-Null
robocopy src/Victoria.Core deploy_pkg/src/Victoria.Core /S /XD bin obj | Out-Null
robocopy src/Victoria.Infrastructure deploy_pkg/src/Victoria.Infrastructure /S /XD bin obj | Out-Null
robocopy src/Victoria.Inventory deploy_pkg/src/Victoria.Inventory /S /XD bin obj | Out-Null

Copy-Item "docker-compose.prod.yml" -Destination "deploy_pkg"
Copy-Item ".env.production" -Destination "deploy_pkg"
Copy-Item "Dockerfile" -Destination "deploy_pkg"

# Comprimir el contenido (sin la carpeta raiz)
Set-Location deploy_pkg
Compress-Archive -Path "*" -DestinationPath ../deploy-package.zip -Force
Set-Location ..
Remove-Item deploy_pkg -Recurse -Force

Write-Host "Subiendo archivos a EC2..." -ForegroundColor Yellow
$DestPackage = $EC2_HOST + ":~/deploy-package.zip"
scp -i $KEY_PATH -o StrictHostKeyChecking=no deploy-package.zip $DestPackage

# 3. Remote Execution
Write-Host "3. Remote Execution..." -ForegroundColor Yellow

$RemoteScript = @"
set -e
echo '>>> Limpieza Profunda de Procesos y Archivos...'
sudo pkill -f dotnet || true

echo '>>> Limpiando instalacion previa...'
sudo rm -rf ~/victoria-wms
mkdir -p ~/victoria-wms

echo '>>> Extracting code...'
unzip -o ~/deploy-package.zip -d ~/victoria-wms
ls -la ~/victoria-wms

echo '>>> Configurando .env...'
if [ -f ~/victoria-wms/.env.production ]; then
    cp ~/victoria-wms/.env.production ~/victoria-wms/.env
    echo ".env created from .env.production"
else
    echo "ERROR: .env.production not found in package"
    exit 1
fi

echo '>>> Limpiando Docker...'
docker rm -f api-perfect worker-perfect victoria-api victoria-worker nginx-proxy || true

echo '>>> Creando base de datos...'
docker run --rm \
  -e PGPASSWORD=vicky_password \
  postgres:15-alpine \
  psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d postgres -c "CREATE DATABASE victoria_perfect;" || true

echo '>>> Rebuild Docker (Construyendo en servidor)...'
cd ~/victoria-wms
docker compose -f docker-compose.prod.yml up -d --build --force-recreate

echo '>>> Esperando inicializacion (45s)...'
sleep 45

echo '>>> [VERIFICACION] Estado de Contenedores:'
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo '>>> [AUDITORIA] Tablas Marten:'
docker run --rm \
  -e PGPASSWORD=vicky_password \
  postgres:15-alpine \
  sh -c 'psql -h victoria-db.ct8iwqe86oz4.us-east-2.rds.amazonaws.com -U vicky_admin -d victoria_perfect -c "\dt mt_doc_*"' || echo "No Marten tables found yet."

echo '>>> [LOGS] Victoria Worker (Marten status):'
docker logs worker-perfect --tail 100 || echo "No logs found for worker-perfect"

echo '>>> Finalizado.'
"@

# Limpiar CRLF
$RemoteScript = $RemoteScript -replace "`r", ""
$ScriptBytes = [System.Text.Encoding]::UTF8.GetBytes($RemoteScript)
$Base64Script = [System.Convert]::ToBase64String($ScriptBytes)

ssh -i $KEY_PATH -o StrictHostKeyChecking=no $EC2_HOST "echo $Base64Script | base64 -d | bash"

Write-Host "DESPLIEGUE FINALIZADO" -ForegroundColor Green
