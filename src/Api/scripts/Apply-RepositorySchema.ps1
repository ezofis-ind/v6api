# Applies repository base schema to a tenant database (idempotent).
# Usage: .\Apply-RepositorySchema.ps1 -Database ezofis_Tenant_1
# Or:    .\Apply-RepositorySchema.ps1 -TenantId "51966bf0-13b9-456e-a4af-1c63d46dbaaf"

param(
    [string]$Server = "EZOFIS_DELL_I9",
    [string]$CatalogDatabase = "ezofis_catalog_new",
    [string]$Database = "",
    [string]$TenantId = "",
    [string]$UserId = "sa",
    [string]$Password = "123@abc"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sqlFile = Join-Path $scriptDir "CreateRepositorySchema.sql"

if ($TenantId -and -not $Database) {
    $catalogConn = "Server=$Server;Database=$CatalogDatabase;User Id=$UserId;Password=$Password;TrustServerCertificate=True;"
    $conn = New-Object System.Data.SqlClient.SqlConnection($catalogConn)
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT ConnectionString FROM catalog.Tenants WHERE Id = @id"
        $cmd.Parameters.AddWithValue("@id", [guid]$TenantId) | Out-Null
        $cs = $cmd.ExecuteScalar()
        if ($cs -match 'Initial Catalog=([^;]+)') { $Database = $Matches[1].Trim() }
        elseif ($cs -match 'Database=([^;]+)') { $Database = $Matches[1].Trim() }
        if (-not $Database) { Write-Error "Could not resolve database from catalog for tenant $TenantId" }
        Write-Host "Resolved tenant DB: $Database"
    } finally { $conn.Close() }
}

if (-not $Database) {
    Write-Error "Specify -Database (e.g. ezofis_Tenant_1) or -TenantId (catalog GUID)"
}

$conn = "Server=$Server;Database=$Database;User Id=$UserId;Password=$Password;TrustServerCertificate=True;"
Write-Host "Applying repository schema to $Database on $Server ..."

sqlcmd -S $Server -d $Database -U $UserId -P $Password -C -i $sqlFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Done. Tables: repository.StorageProviders, repository.Repositories, repository.RepositoryFields, ..."
