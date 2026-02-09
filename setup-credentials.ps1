
$settingsPath = "src/Victoria.API/appsettings.Development.json"

if (-not (Test-Path $settingsPath)) {
    Write-Error "File not found: $settingsPath"
    exit 1
}

$json = Get-Content $settingsPath | Out-String | ConvertFrom-Json

Write-Host "--------------------------------------------------" -ForegroundColor Cyan
Write-Host "VICTORIA WMS - MANUAL CREDENTIAL SETUP" -ForegroundColor Cyan
Write-Host "--------------------------------------------------" -ForegroundColor Cyan

$url = Read-Host "Ingrese Odoo URL (e.g. https://my-odoo.com)"
$db = Read-Host "Ingrese Database Name"
$user = Read-Host "Ingrese Odoo User (Email)"
$apiKey = Read-Host "Ingrese Odoo Password / API Key"

if ([string]::IsNullOrWhiteSpace($url) -or [string]::IsNullOrWhiteSpace($db)) {
    Write-Error "URL and DB are required."
    exit 1
}

# Update JSON object
$json.Odoo.Url = $url
$json.Odoo.Db = $db
$json.Odoo.User = $user
$json.Odoo.ApiKey = $apiKey

# Ensure Polling is Enabled
$json.ENABLE_ODOO_POLLING = $true
$json.Odoo.PollingIntervalSeconds = 10

# Save back to file
$json | ConvertTo-Json -Depth 10 | Set-Content $settingsPath

Write-Host "--------------------------------------------------" -ForegroundColor Green
Write-Host "SUCCESS: Credentials updated in $settingsPath" -ForegroundColor Green
Write-Host "--------------------------------------------------" -ForegroundColor Green
