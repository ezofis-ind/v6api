# Quick table verification - lists key tables in Catalog and Tenant DBs
# Usage: .\Verify-Tables.ps1
#        .\Verify-Tables.ps1 -TenantId "guid"   (checks tenant DB from catalog)
#        .\Verify-Tables.ps1 -TenantDatabase "ezofis_Tenant_1"

param(
    [string]$Server = "ezmtraildb.database.windows.net",
    [string]$User = "ezmtrailsa",
    [string]$Password = "Ezofis@123",
    [string]$CatalogDatabase = "ezofis_catalog_Dev",
    [string]$TenantId = "",
    [string]$TenantDatabase = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName "System.Data"

$catalogConn = "Server=$Server;Database=$CatalogDatabase;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

function Invoke-Query {
    param([string]$ConnStr, [string]$Sql)
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnStr)
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $dt = New-Object System.Data.DataTable
        [void]$adapter.Fill($dt)
        return $dt
    } finally { $conn.Close() }
}

# Resolve tenant DB from catalog if TenantId provided
if ($TenantId -and -not $TenantDatabase) {
    $dt = Invoke-Query -ConnStr $catalogConn -Sql "SELECT ConnectionString FROM catalog.Tenants WHERE Id = '$TenantId'"
    if ($dt.Rows.Count -gt 0) {
        $cs = $dt.Rows[0][0]
        if ($cs -match 'Initial Catalog=([^;]+)') { $TenantDatabase = $Matches[1].Trim() }
        elseif ($cs -match 'Database=([^;]+)') { $TenantDatabase = $Matches[1].Trim() }
    }
}
$tenantConn = "Server=$Server;Database=$TenantDatabase;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "`n=== Table Verification ===" -ForegroundColor Cyan

# Catalog
Write-Host "`n[CATALOG] $CatalogDatabase" -ForegroundColor Yellow
$catalogTables = Invoke-Query -ConnStr $catalogConn -Sql @"
SELECT s.name + '.' + t.name AS [Table], t.create_date AS [Created]
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name
"@
$catalogTables | ForEach-Object { $_.Table }
$keyCatalog = @('catalog.Tenants', 'catalog.UserTenants')
$found = ($catalogTables | Where-Object { $keyCatalog -contains $_.Table }).Count
Write-Host "Key tables (catalog.Tenants, catalog.UserTenants): $(if ($found -eq 2) { 'OK' } else { 'MISSING' })" -ForegroundColor $(if ($found -eq 2) { 'Green' } else { 'Red' })

# Tenant (if specified)
if ($TenantDatabase) {
    Write-Host "`n[TENANT] $TenantDatabase" -ForegroundColor Yellow
    $tenantTables = Invoke-Query -ConnStr $tenantConn -Sql @"
SELECT s.name + '.' + t.name AS [Table], t.create_date AS [Created]
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name
"@
    $tenantTables | ForEach-Object { $_.Table }
    $keyTenant = @('workflow.Workflows', 'workflow.WorkflowInstances', 'workflow.WorkflowStepInstances', 'users.Users')
    $foundTenant = ($tenantTables | Where-Object { $keyTenant -contains $_.Table }).Count
    Write-Host "Key tables (workflow.Workflows, workflow.WorkflowInstances, users.Users): $(if ($foundTenant -ge 3) { 'OK' } else { 'MISSING' })" -ForegroundColor $(if ($foundTenant -ge 3) { 'Green' } else { 'Red' })
} else {
    Write-Host "`n[TENANT] Skipped - use -TenantId or -TenantDatabase to check tenant tables" -ForegroundColor Gray
}

Write-Host ""
