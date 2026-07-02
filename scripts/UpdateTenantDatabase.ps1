# Apply Users module migrations to a tenant database.
# Resolve tenant connection string from catalog.Tenants, or set env:TenantConnectionString.
#
# Examples:
#   .\scripts\UpdateTenantDatabase.ps1 -CatalogDatabase ezofis_catalog_dev
#   .\scripts\UpdateTenantDatabase.ps1 -CatalogDatabase ezofis_catalog_dev -TenantId "<guid>"
#   .\scripts\UpdateTenantDatabase.ps1 -CatalogDatabase ezofis_catalog_dev -AllTenants
#   $env:TenantConnectionString = '...'; .\scripts\UpdateTenantDatabase.ps1

param(
    [string]$Server = "",
    [string]$CatalogDatabase = "ezofis_catalog_dev",
    [string]$CatalogConnectionString = "",
    [string]$TenantId = "",
    [switch]$AllTenants,
    [string]$UserId = "",
    [string]$Password = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet tool restore | Out-Null

function Get-CatalogConnectionString {
    if ($env:CatalogConnectionString) { return $env:CatalogConnectionString }
    if ($CatalogConnectionString) { return $CatalogConnectionString }

    $appSettingsPath = Join-Path $root "src\Api/appsettings.json"
    if (-not (Test-Path $appSettingsPath)) {
        $appSettingsPath = Join-Path $root "src/Api/appsettings.Development.json"
    }
    if (Test-Path $appSettingsPath) {
        $json = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        if ($json.ConnectionStrings.DefaultConnection) {
            return $json.ConnectionStrings.DefaultConnection
        }
    }

    if (-not $Server) { $Server = "DESKTOP-BEQ1IBQ" }
    if (-not $UserId) {
        return "Server=$Server;Database=$CatalogDatabase;Integrated Security=True;TrustServerCertificate=True;"
    }
    if (-not $Password) {
        Write-Host "Set -Password, env:CatalogConnectionString, or src/Api/appsettings.json ConnectionStrings:DefaultConnection." -ForegroundColor Yellow
        exit 1
    }

    return "Server=$Server;Database=$CatalogDatabase;User Id=$UserId;Password=$Password;TrustServerCertificate=True;"
}

function Get-TenantConnectionStringsFromCatalog {
    param([string]$CatalogConn, [string]$TenantIdFilter, [switch]$All)

    $connection = New-Object System.Data.SqlClient.SqlConnection($CatalogConn)
    $connection.Open()
    try {
        $cmd = $connection.CreateCommand()
        if ($TenantIdFilter) {
            $cmd.CommandText = "SELECT Id, Name, ConnectionString FROM catalog.Tenants WHERE Id = @id"
            $cmd.Parameters.AddWithValue("@id", [guid]$TenantIdFilter) | Out-Null
        }
        elseif ($All) {
            $cmd.CommandText = "SELECT Id, Name, ConnectionString FROM catalog.Tenants ORDER BY Name"
        }
        else {
            $cmd.CommandText = "SELECT TOP 1 Id, Name, ConnectionString FROM catalog.Tenants ORDER BY Name"
        }

        $reader = $cmd.ExecuteReader()
        $rows = @()
        while ($reader.Read()) {
            $rows += [pscustomobject]@{
                Id = $reader["Id"].ToString()
                Name = $reader["Name"].ToString()
                ConnectionString = $reader["ConnectionString"].ToString()
            }
        }
        $reader.Close()
        return $rows
    }
    finally {
        $connection.Close()
    }
}

function Invoke-UsersDatabaseUpdate {
    param([string]$TenantConnectionString, [string]$TenantLabel)

    Write-Host ""
    Write-Host "Applying Users migrations for $TenantLabel ..." -ForegroundColor Cyan

    $env:ConnectionStrings__DefaultConnection = $TenantConnectionString
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    dotnet ef database update `
        --context UsersDbContext `
        --project src\Modules\Users\Users.Infrastructure\SaaSApp.Users.Infrastructure.csproj `
        --startup-project src\Api\SaaSApp.Api.csproj

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "Tenant database updated: $TenantLabel" -ForegroundColor Green
}

if ($env:TenantConnectionString) {
    Invoke-UsersDatabaseUpdate -TenantConnectionString $env:TenantConnectionString -TenantLabel "env:TenantConnectionString"
    exit 0
}

$catalogConn = Get-CatalogConnectionString
Write-Host "Reading tenant connection string(s) from catalog ..." -ForegroundColor Cyan

$tenants = Get-TenantConnectionStringsFromCatalog -CatalogConn $catalogConn -TenantIdFilter $TenantId -All:$AllTenants
if ($tenants.Count -eq 0) {
    Write-Host "No tenant found in catalog.Tenants." -ForegroundColor Red
    exit 1
}

foreach ($tenant in $tenants) {
    if ([string]::IsNullOrWhiteSpace($tenant.ConnectionString)) {
        Write-Host "Skipping $($tenant.Name) ($($tenant.Id)): empty ConnectionString." -ForegroundColor Yellow
        continue
    }
    Invoke-UsersDatabaseUpdate -TenantConnectionString $tenant.ConnectionString -TenantLabel "$($tenant.Name) ($($tenant.Id))"
}

Write-Host ""
Write-Host "All requested tenant database updates completed." -ForegroundColor Green
