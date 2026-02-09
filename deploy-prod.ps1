$ErrorActionPreference = "Stop"

Write-Host "INICIANDO DESPLIEGUE A PRODUCCION (Full Stack - EC2)" -ForegroundColor Cyan

# Variables
$EC2_HOST = "ec2-user@3.14.182.244"
$KEY_PATH = ".\victoria-key-fixed.pem"

# 1. Frontend Build
Write-Host "1. Frontend Build..." -ForegroundColor Yellow
Set-Location "src/Victoria.UI"
$env:VITE_API_URL = "https://app.victoriawms.dev/api/v1"
npm run build
if ($LASTEXITCODE -ne 0) { Write-Error "Build Failed"; exit 1 }
Set-Location ../..

# 2. Package Creation
Write-Host "2. Creating Package..." -ForegroundColor Yellow
if (Test-Path deploy-package.zip) { Remove-Item deploy-package.zip }
if (Test-Path deploy_pkg) { Remove-Item deploy_pkg -Recurse -Force }

New-Item -ItemType Directory -Path "deploy_pkg/src" | Out-Null
robocopy src/Victoria.API deploy_pkg/src/Victoria.API /S /XD bin obj | Out-Null
robocopy src/Victoria.Core deploy_pkg/src/Victoria.Core /S /XD bin obj | Out-Null
robocopy src/Victoria.Infrastructure deploy_pkg/src/Victoria.Infrastructure /S /XD bin obj | Out-Null
robocopy src/Victoria.Inventory deploy_pkg/src/Victoria.Inventory /S /XD bin obj | Out-Null

# Include Frontend Dist
Copy-Item "src/Victoria.UI/dist" -Destination "deploy_pkg/dist" -Recurse

Copy-Item "docker-compose.prod.yml" -Destination "deploy_pkg"
Copy-Item ".env.production" -Destination "deploy_pkg"
Copy-Item "Dockerfile" -Destination "deploy_pkg"
Copy-Item "nginx.conf" -Destination "deploy_pkg"

# Compress
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
echo '>>> Limpieza inicial...'
# Preserve certbot if exists
if [ -d ~/victoria-wms/certbot ]; then
    echo 'Preserving certbot directory...'
    sudo mv ~/victoria-wms/certbot ~/victoria-wms-certbot-tmp
fi
sudo rm -rf ~/victoria-wms
mkdir -p ~/victoria-wms
if [ -d ~/victoria-wms-certbot-tmp ]; then
    sudo mv ~/victoria-wms-certbot-tmp ~/victoria-wms/certbot
fi

echo '>>> Extracting code...'
unzip -o ~/deploy-package.zip -d ~/victoria-wms
ls -la ~/victoria-wms

echo '>>> Configurando .env...'
if [ -f ~/victoria-wms/.env.production ]; then
    cp ~/victoria-wms/.env.production ~/victoria-wms/.env
else
    echo "ERROR: .env.production not found"
    exit 1
fi

echo '>>> Rebuild Docker...'
cd ~/victoria-wms
# Forzamos rebuild de la API y recreacion de contenedores (incluyendo nginx con nuevo volumen)
sudo docker compose -f docker-compose.prod.yml up -d --build --force-recreate

echo '>>> Esperando inicializacion (20s)...'
sleep 20

echo '>>> [VERIFICACION] Estado de Contenedores:'
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo '>>> Aplicando permisos Anti-500 (Nginx Assets)...'
sudo chown -R 101:101 ~/victoria-wms/dist
sudo chmod -R 755 ~/victoria-wms/dist

echo '>>> [VERIFICACION] Frontend Asset Check:'
if [ -d ~/victoria-wms/dist ]; then
    echo "✅ Frontend dist folder present on host."
else
    echo "❌ Frontend dist folder MISSING on host."
fi

echo '>>> Finalizado.'
"@

# Limpiar CRLF
$RemoteScript = $RemoteScript -replace "`r", ""
$ScriptBytes = [System.Text.Encoding]::UTF8.GetBytes($RemoteScript)
$Base64Script = [System.Convert]::ToBase64String($ScriptBytes)

ssh -i $KEY_PATH -o StrictHostKeyChecking=no $EC2_HOST "echo $Base64Script | base64 -d | bash"

Write-Host "DESPLIEGUE FINALIZADO" -ForegroundColor Green

