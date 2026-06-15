# Apply workflow schema to an existing tenant database
# Use when tenant was created without workflow tables (e.g. workflow.Workflows missing)
#
# Usage: .\ApplyWorkflowSchemaToTenant.ps1 -TenantDatabase "ezofis_Tenant_1"
# Or:    .\ApplyWorkflowSchemaToTenant.ps1 -TenantId "fccf34b5-5588-4334-869a-e4c7b10b244d"
#        (queries catalog for connection string)

param(
    [string]$Server = "ezmtraildb.database.windows.net",
    [string]$User = "ezmtrailsa",
    [string]$Password = "Ezofis@123",
    [string]$CatalogDatabase = "ezofis_catalog_Dev",
    [string]$TenantDatabase = "",
    [string]$TenantId = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Add-Type -AssemblyName "System.Data"

$catalogConn = "Server=$Server;Database=$CatalogDatabase;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$tenantConn = "Server=$Server;Database=$TenantDatabase;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# Resolve tenant database from catalog if TenantId provided
if ($TenantId -and -not $TenantDatabase) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($catalogConn)
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT ConnectionString FROM catalog.Tenants WHERE Id = @id"
        $cmd.Parameters.AddWithValue("@id", [guid]$TenantId) | Out-Null
        $cs = $cmd.ExecuteScalar()
        if ($cs) {
            # Parse database name (avoid SqlConnectionStringBuilder - it rejects MultipleActiveResultSets)
            if ($cs -match 'Initial Catalog=([^;]+)') { $TenantDatabase = $Matches[1].Trim() }
            elseif ($cs -match 'Database=([^;]+)') { $TenantDatabase = $Matches[1].Trim() }
            if (-not $TenantDatabase) {
                Write-Host "Could not parse database name from connection string" -ForegroundColor Red
                exit 1
            }
            Write-Host "Resolved tenant DB: $TenantDatabase" -ForegroundColor Cyan
        } else {
            Write-Host "Tenant not found in catalog: $TenantId" -ForegroundColor Red
            exit 1
        }
    } finally { $conn.Close() }
    $tenantConn = "Server=$Server;Database=$TenantDatabase;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
}

if (-not $TenantDatabase) {
    Write-Host "Usage: -TenantDatabase 'ezofis_Tenant_1' OR -TenantId 'guid'" -ForegroundColor Red
    exit 1
}

function Run-SqlBatch {
    param([string]$ConnStr, [string]$Sql)
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnStr)
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        $cmd.CommandTimeout = 120
        $cmd.ExecuteNonQuery() | Out-Null
    } finally { $conn.Close() }
}

function Run-SqlFile {
    param([string]$ConnStr, [string]$FilePath)
    $content = Get-Content $FilePath -Raw -Encoding UTF8
    $batches = [regex]::Split($content, '(?m)^\s*GO\s*$') | Where-Object { $_.Trim().Length -gt 0 }
    foreach ($batch in $batches) {
        $b = $batch.Trim()
        if ($b.Length -lt 10) { continue }  # Skip trivial batches; do NOT skip batches starting with --
        try {
            Run-SqlBatch -ConnStr $ConnStr -Sql $b
            Write-Host "  OK" -ForegroundColor Green
        } catch {
            Write-Host "  Warning: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

Write-Host "`n=== Apply Workflow Schema to Tenant ===" -ForegroundColor Cyan
Write-Host "Database: $TenantDatabase`n" -ForegroundColor Yellow

Run-SqlFile -ConnStr $tenantConn -FilePath "$scriptDir\CreateWorkflowSchemaComplete.sql"

Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host "Workflow schema applied. workflow.Workflows and related tables are ready.`n" -ForegroundColor White
