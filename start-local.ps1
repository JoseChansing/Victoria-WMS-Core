# Start Local Infrastructure
Write-Host "Starting Local Infrastructure (Postgres + Redis)..." -ForegroundColor Cyan
docker compose -f docker-compose.local.yml up -d

Write-Host "`n------------------------------------------------------------" -ForegroundColor Green
Write-Host "ENVIRONMENT READY" -ForegroundColor Green
Write-Host "------------------------------------------------------------" -ForegroundColor Green
Write-Host "1. To START BACKEND (New Terminal):"
Write-Host "   `$env:ASPNETCORE_ENVIRONMENT='Development'"
Write-Host "   cd src/Victoria.API"
Write-Host "   dotnet watch run"
Write-Host "`n2. To START FRONTEND (New Terminal):"
Write-Host "   cd src/Victoria.UI"
Write-Host "   npm run dev"
Write-Host "------------------------------------------------------------" -ForegroundColor Green
