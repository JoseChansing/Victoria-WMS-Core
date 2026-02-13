# Startup Script for Victoria WMS Core (Local Dev)
Write-Host "🚀 Starting Victoria WMS Development Environment..." -ForegroundColor Green

# 1. Start Docker Containers
Write-Host "🐳 Checking Docker containers..." -ForegroundColor Cyan
docker-compose up -d
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to start Docker containers. Make sure Docker Desktop is running." -ForegroundColor Red
    exit
}
Write-Host "✅ Docker containers are up." -ForegroundColor Green

# 2. Start Backend (API)
Write-Host "🔙 Starting Backend (API)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'src/Victoria.API'; dotnet run" -WorkingDirectory "$PSScriptRoot"
Write-Host "✅ Backend started in new window." -ForegroundColor Green

# 3. Start Frontend (UI)
Write-Host "FRONT Starting Frontend (UI)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'src/Victoria.UI'; npm run dev" -WorkingDirectory "$PSScriptRoot"
Write-Host "✅ Frontend started in new window." -ForegroundColor Green

Write-Host "Environment is ready!" -ForegroundColor Yellow
