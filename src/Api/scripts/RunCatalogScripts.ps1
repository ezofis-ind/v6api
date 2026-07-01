# Run Catalog DB scripts - no sqlcmd required
# Uses .NET SqlClient to execute scripts
#
# Usage: .\RunCatalogScripts.ps1
# For different server: .\RunCatalogScripts.ps1 -Server "yourserver.database.windows.net" -User "user" -Password "pass"

param(
    [string]$Server = "ezmtraildb.database.windows.net",
    [string]$User = "ezmtrailsa",
    [string]$Password = "Ezofis@123",
    [string]$Database = "ezofis_catalog_Dev"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

# Load SqlClient (works on Windows PowerShell and PowerShell 7)
Add-Type -AssemblyName "System.Data"

$masterConn = "Server=$Server;Database=master;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$catalogConn = "Server=$Server;Database=$Database;User Id=$User;Password=$Password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

function Run-SqlBatch {
    param([string]$ConnStr, [string]$Sql)
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnStr)
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        $cmd.CommandTimeout = 300
        $cmd.ExecuteNonQuery() | Out-Null
    } finally {
        $conn.Close()
    }
}

function Run-SqlFile {
    param([string]$ConnStr, [string]$FilePath)
    $content = Get-Content $FilePath -Raw -Encoding UTF8
    # Split on GO (line containing only GO) - (?m) = multiline so ^$ match line boundaries
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

Write-Host "`n=== Catalog Database Setup ===" -ForegroundColor Cyan
Write-Host "Server: $Server | Database: $Database`n" -ForegroundColor Yellow

# Step 1: Create database
Write-Host "Step 1: Creating database..." -ForegroundColor Cyan
try {
    Run-SqlBatch -ConnStr $masterConn -Sql "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '$Database') CREATE DATABASE [$Database];"
    Write-Host "  OK: Database ready`n" -ForegroundColor Green
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Catalog tables
Write-Host "Step 2: Creating Tenants, UserTenants tables..." -ForegroundColor Cyan
Run-SqlFile -ConnStr $catalogConn -FilePath "$scriptDir\01b_CreateCatalogTables.sql"
Write-Host ""

# Step 3: Hangfire (no GO - run as single batch)
Write-Host "Step 3: Installing Hangfire..." -ForegroundColor Cyan
try {
    $hangfireSql = Get-Content "$scriptDir\01c_InstallHangfire.sql" -Raw
    Run-SqlBatch -ConnStr $catalogConn -Sql $hangfireSql
    Write-Host "  OK: Hangfire installed`n" -ForegroundColor Green
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host "=== Done ===" -ForegroundColor Cyan
Write-Host "Catalog database is ready. Start with: dotnet run --project src/Api`n" -ForegroundColor White
