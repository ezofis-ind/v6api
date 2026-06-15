# Create catalog database and schema (catalog.Tenants) on Azure SQL.
# DefaultConnection in appsettings = catalog DB (e.g. ezofis_catalog_Dev for Development, ezofis_catalog for Production).
# Run from repo root: .\scripts\CreateDatabase.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$manifestPath = Join-Path $root ".config\dotnet-tools.json"
if (Test-Path $manifestPath) {
  Write-Host "Restoring dotnet-ef tool (local manifest)..." -ForegroundColor Cyan
  dotnet tool restore --tool-manifest $manifestPath
}
# Ensure dotnet-ef is available globally (so we can call it by path and avoid local-manifest precedence)
Write-Host "Ensuring dotnet-ef is available..." -ForegroundColor Cyan
dotnet tool install --global dotnet-ef --version 8.0.11 2>$null

# Use global tool by path so "dotnet ef" does not resolve to local manifest
$dotnetEf = Join-Path $env:USERPROFILE ".dotnet\tools\dotnet-ef.exe"
if (-not (Test-Path $dotnetEf)) {
  Write-Host "dotnet-ef not found at $dotnetEf" -ForegroundColor Red
  exit 1
}

Write-Host "Applying catalog migrations (Tenants table + Email, SignupSource, Platform, AppVersion)..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Development"
& $dotnetEf database update `
  --project src\BuildingBlocks\Catalog\SaaSApp.Catalog.csproj `
  --startup-project src\Api\SaaSApp.Api.csproj `
  --context CatalogDbContext

if ($LASTEXITCODE -eq 0) {
  Write-Host "Catalog database created/updated successfully." -ForegroundColor Green
  Write-Host "Next: create each tenant DB in Azure, run .\scripts\UpdateTenantDatabase.ps1 with its connection string, then POST /api/admin/tenants to register." -ForegroundColor Cyan
} else {
  Write-Host "Migration failed. Check connection string and Azure SQL firewall." -ForegroundColor Red
  exit 1
}
