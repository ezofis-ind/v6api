# Reset Catalog Database - Drop and recreate for fresh start
# Use when you want to remove all tenants and start over.
# WARNING: This deletes all tenant registrations. Tenant DBs are NOT dropped.
#
# Usage: .\ResetCatalog.ps1 -FromAppSettings
#        .\ResetCatalog.ps1 -Server "ezmtraildb.database.windows.net" -Database "ezofis_catalog_Dev" -User "ezmtrailsa" -Password "xxx"
#        .\ResetCatalog.ps1 -Server "localhost" -Database "ezofis_catalog_Dev"  # Windows auth for local SQL

param(
    [string]$Server = "localhost",
    [string]$Database = "ezofis_catalog_Dev",
    [string]$User = "",
    [string]$Password = "",
    [switch]$FromAppSettings,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

# Load from appsettings.Development.json if requested
if ($FromAppSettings) {
    $appSettingsPath = Join-Path (Split-Path $scriptDir -Parent) "src\Api\appsettings.Development.json"
    if (-not (Test-Path $appSettingsPath)) { $appSettingsPath = Join-Path $scriptDir "..\src\Api\appsettings.Development.json" }
    if (Test-Path $appSettingsPath) {
        $json = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        $cs = $json.ConnectionStrings.DefaultConnection
        if ($cs -match "Data Source=([^;]+)") { $Server = $Matches[1].Trim() }
        if ($cs -match "Initial Catalog=([^;]+)") { $Database = $Matches[1].Trim() }
        if ($cs -match "User ID=([^;]+)") { $User = $Matches[1].Trim() }
        if ($cs -match "Password=([^;]+)") { $Password = $Matches[1].Trim() }
        Write-Host "Loaded from appsettings.Development.json: Server=$Server, Database=$Database" -ForegroundColor Gray
    } else {
        Write-Host "appsettings.Development.json not found at $appSettingsPath" -ForegroundColor Red
        exit 1
    }
}

# Connect to master to drop/create database
if ($User -and $Password) {
    $connStr = "Server=$Server;Initial Catalog=master;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
} else {
    $connStr = "Server=$Server;Initial Catalog=master;Integrated Security=true;Connection Timeout=30;"
}

Write-Host "`n=== Reset Catalog Database ===" -ForegroundColor Cyan
Write-Host "Server: $Server | Database: $Database" -ForegroundColor Yellow
if ($WhatIf) {
    Write-Host "WhatIf: Would drop and recreate catalog. Run without -WhatIf to execute." -ForegroundColor Gray
    exit 0
}

Write-Host "`nWARNING: This will DROP the catalog database and recreate it." -ForegroundColor Red
Write-Host "All tenant registrations will be lost. Tenant databases are NOT dropped.`n" -ForegroundColor Yellow

$confirm = Read-Host "Type 'yes' to continue"
if ($confirm -ne "yes") {
    Write-Host "Aborted." -ForegroundColor Gray
    exit 0
}

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()

    # Drop database
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
IF EXISTS (SELECT * FROM sys.databases WHERE name = '$Database')
BEGIN
    ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$Database];
    PRINT 'Dropped database: $Database';
END
"@
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "  OK: Database dropped" -ForegroundColor Green

    # Create database
    $cmd.CommandText = "CREATE DATABASE [$Database]"
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "  OK: Database created" -ForegroundColor Green

    $conn.Close()
} catch {
    Write-Host "  FAIL: $_" -ForegroundColor Red
    exit 1
}

# Apply catalog tables (01b) and Hangfire (01c)
Write-Host "`nApplying catalog tables (01b_CreateCatalogTables.sql)..." -ForegroundColor Cyan
$sqlcmdArgs = @("-S", $Server, "-d", $Database, "-i", "$scriptDir\01b_CreateCatalogTables.sql", "-b")
if ($User -and $Password) { $sqlcmdArgs += @("-U", $User, "-P", $Password) } else { $sqlcmdArgs += "-E" }
& sqlcmd @sqlcmdArgs 2>$null
if ($LASTEXITCODE -eq 0) { Write-Host "  OK: Catalog tables created" -ForegroundColor Green } else { Write-Host "  Run: sqlcmd -S $Server -d $Database -E -i scripts\01b_CreateCatalogTables.sql" -ForegroundColor Yellow }

Write-Host "`nApplying Hangfire (01c_InstallHangfire.sql)..." -ForegroundColor Cyan
$sqlcmdArgs = @("-S", $Server, "-d", $Database, "-i", "$scriptDir\01c_InstallHangfire.sql", "-b")
if ($User -and $Password) { $sqlcmdArgs += @("-U", $User, "-P", $Password) } else { $sqlcmdArgs += "-E" }
& sqlcmd @sqlcmdArgs 2>$null
if ($LASTEXITCODE -eq 0) { Write-Host "  OK: Hangfire installed" -ForegroundColor Green } else { Write-Host "  Run: sqlcmd -S $Server -d $Database -E -i scripts\01c_InstallHangfire.sql" -ForegroundColor Yellow }

Write-Host "`n=== Catalog Reset Complete ===" -ForegroundColor Cyan
Write-Host "Next: Use Signup API to create new tenants. Workflow + DMS schema created automatically on signup.`n" -ForegroundColor Gray
