# Start Local Infrastructure
Write-Host "Starting Local Infrastructure (Postgres + Redis)..." -ForegroundColor Cyan
docker compose -f docker-compose.local.yml up -d

Write-Host "`n------------------------------------------------------------" -ForegroundColor Green
Write-Host "ENVIRONMENT READY" -ForegroundColor Green
Write-Host "------------------------------------------------------------" -ForegroundColor Green
Write-Host "1. To START BACKEND (New Terminal):"
Write-Host "   `$env:ASPNETCORE_ENVIRONMENT='Development'"
Write-Host "   cd src/Victoria.API"
Write-Host "   dotnet run" 
Write-Host "   # NOTE: API MUST listen on http://localhost:5242 for Frontend Proxy to work."
Write-Host "   # If it starts on port 5000, 'launchSettings.json' is missing or ignored."

Write-Host "`n2. To START FRONTEND (New Terminal):"
Write-Host "   cd src/Victoria.UI"
Write-Host "   # Windows CMD: cmd /c 'npm run dev'"
Write-Host "   npm run dev"
Write-Host "------------------------------------------------------------" -ForegroundColor Green
