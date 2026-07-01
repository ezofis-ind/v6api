# Apply schema updates for existing deployments
# Run this if you have existing catalog/tenant DBs before testing new signup
#
# Usage: .\Apply-SchemaUpdates.ps1
#        .\Apply-SchemaUpdates.ps1 -CatalogOnly
#        .\Apply-SchemaUpdates.ps1 -TenantId "guid"

param(
    [string]$Server = "ezmtraildb.database.windows.net",
    [string]$User = "ezmtrailsa",
    [string]$Password = "Ezofis@123",
    [string]$CatalogDatabase = "ezofis_catalog_Dev",
    [string]$TenantId = "",
    [switch]$CatalogOnly
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

# Catalog: Add IsSuperuser to UserTenants
$catalogConn = "Server=$Server;Database=$CatalogDatabase;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$catalogSql = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('catalog.UserTenants') AND name = 'IsSuperuser')
BEGIN
    ALTER TABLE [catalog].[UserTenants] ADD [IsSuperuser] BIT NOT NULL DEFAULT 0;
    PRINT 'IsSuperuser column added to catalog.UserTenants';
END
ELSE
    PRINT 'IsSuperuser already exists';
"@

Write-Host "`n=== Applying Schema Updates ===" -ForegroundColor Cyan

# Apply catalog update
Write-Host "`n[Catalog] Adding IsSuperuser to UserTenants..." -ForegroundColor Yellow
try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($catalogConn)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $catalogSql
    $cmd.ExecuteNonQuery() | Out-Null
    $conn.Close()
    Write-Host "  OK: Catalog updated" -ForegroundColor Green
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

# Tenant: Re-run workflow schema for existing tenants (adds new columns via ALTER)
if (-not $CatalogOnly -and $TenantId) {
    Write-Host "`n[Tenant] Run: .\ApplyWorkflowSchemaToTenant.ps1 -TenantId $TenantId" -ForegroundColor Yellow
    Write-Host "[Tenant] For DMS StagingItems (temp indexing): Run CreateDmsSchema.sql or signup creates it." -ForegroundColor Gray
}

Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host "For NEW signup: Rebuild API (dotnet build), then signup. New tenants get full schema." -ForegroundColor Gray
Write-Host "For existing catalog: Run this script to add IsSuperuser." -ForegroundColor Gray
Write-Host ""
