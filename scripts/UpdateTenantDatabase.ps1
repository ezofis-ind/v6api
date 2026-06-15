# Apply Users module migrations to a tenant database.
# 1. Create the tenant DB in Azure (e.g. ezofis_Tenant_1, ezofis_Tenant_2) or use Signup API to create it.
# 2. Set env TenantConnectionString to that DB's connection string.
# 3. Run this script. Then register tenant: POST /api/admin/tenants with Id (JWT tid), Name, ConnectionString.
# Run from repo root.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $env:TenantConnectionString) {
  Write-Host "Set env:TenantConnectionString to the tenant DB connection string." -ForegroundColor Yellow
  Write-Host "Example: `$env:TenantConnectionString = 'Data Source=...;Initial Catalog=ezofis_Tenant_1;User ID=...;Password=...;Encrypt=True;...'" -ForegroundColor Gray
  exit 1
}

$dotnetEf = Join-Path $env:USERPROFILE ".dotnet\tools\dotnet-ef.exe"
if (-not (Test-Path $dotnetEf)) {
  Write-Host "Install dotnet-ef globally: dotnet tool install --global dotnet-ef --version 8.0.11" -ForegroundColor Yellow
  exit 1
}
$env:ConnectionStrings__DefaultConnection = $env:TenantConnectionString
$env:ASPNETCORE_ENVIRONMENT = "Development"
& $dotnetEf database update `
  --project src\Modules\Users\Users.Infrastructure\SaaSApp.Users.Infrastructure.csproj `
  --startup-project src\Api\SaaSApp.Api.csproj

if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "Tenant database updated. Register tenant via POST /api/admin/tenants (Admin auth)." -ForegroundColor Green
