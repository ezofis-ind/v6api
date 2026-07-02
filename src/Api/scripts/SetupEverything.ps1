# =============================================
# MASTER SETUP SCRIPT - Creates Everything
# Run this to set up catalog + first tenant database
# =============================================

param(
    [string]$ServerName = "localhost",
    [string]$TenantDatabaseName = "ezofis_Tenant_1"
)

Write-Host "=== Master Database Setup ===" -ForegroundColor Cyan
Write-Host "Server: $ServerName" -ForegroundColor Yellow
Write-Host "Tenant Database: $TenantDatabaseName" -ForegroundColor Yellow
Write-Host ""

# Check if sqlcmd is available
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host "✗ ERROR: sqlcmd not found!" -ForegroundColor Red
    Write-Host "Please install SQL Server Command Line Tools" -ForegroundColor Red
    exit 1
}

# Step 1: Create Catalog Database
Write-Host "Step 1: Creating Catalog Database..." -ForegroundColor Cyan
try {
    sqlcmd -S $ServerName -d master -i "01a_CreateCatalogDatabase.sql" -o "catalog_setup.log"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Catalog database created successfully" -ForegroundColor Green
    } else {
        Write-Host "⚠ Catalog setup completed with warnings. Check catalog_setup.log" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Catalog setup failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host "Creating Catalog Tables..." -ForegroundColor Cyan
try {
    sqlcmd -S $ServerName -d "ezofis_catalog_Dev" -i "01b_CreateCatalogTables.sql" -o "catalog_tables.log"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Catalog tables created successfully" -ForegroundColor Green
    } else {
        Write-Host "⚠ Catalog tables setup completed with warnings. Check catalog_tables.log" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Catalog tables setup failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host "Installing Hangfire schema..." -ForegroundColor Cyan
try {
    sqlcmd -S $ServerName -d "ezofis_catalog_Dev" -i "01c_InstallHangfire.sql" -o "hangfire_install.log"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Hangfire schema installed successfully" -ForegroundColor Green
    } else {
        Write-Host "⚠ Hangfire install completed with warnings. Check hangfire_install.log" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Hangfire install failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 2: Create Tenant Database
Write-Host "Step 2: Creating Tenant Database..." -ForegroundColor Cyan
Write-Host "⚠ IMPORTANT: Make sure you edited 02_CreateTenantDatabase.sql" -ForegroundColor Yellow
Write-Host "   Change the database name on lines 8 and 36 to: $TenantDatabaseName" -ForegroundColor Yellow
Write-Host ""
Write-Host "Press Enter to continue or Ctrl+C to cancel..." -ForegroundColor Yellow
Read-Host

try {
    sqlcmd -S $ServerName -i "02_CreateTenantDatabase.sql" -o "tenant_setup.log"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Tenant database created successfully" -ForegroundColor Green
    } else {
        Write-Host "⚠ Tenant setup completed with warnings. Check tenant_setup.log" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Tenant setup failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 3: Verification
Write-Host "Step 3: Verifying Setup..." -ForegroundColor Cyan

# Check catalog
Write-Host "Checking catalog database..." -ForegroundColor Yellow
$catalogCheck = sqlcmd -S $ServerName -d "ezofis_catalog_Dev" -Q "SELECT COUNT(*) AS TableCount FROM sys.tables WHERE is_ms_shipped = 0" -h -1
Write-Host "  Catalog tables: $catalogCheck (Expected: 3)" -ForegroundColor White

# Check tenant
Write-Host "Checking tenant database..." -ForegroundColor Yellow
$tenantCheck = sqlcmd -S $ServerName -d $TenantDatabaseName -Q "SELECT COUNT(*) AS TableCount FROM sys.tables WHERE schema_id IN (SCHEMA_ID('users'), SCHEMA_ID('workflow'))" -h -1
Write-Host "  Tenant tables: $tenantCheck (Expected: 8+)" -ForegroundColor White

Write-Host ""

# Summary
Write-Host "=== Setup Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "✓ Catalog Database: ezofis_catalog_Dev" -ForegroundColor Green
Write-Host "✓ Tenant Database: $TenantDatabaseName" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Register tenant in catalog (or use signup API)" -ForegroundColor White
Write-Host "2. Start your API: cd src/Api && dotnet run" -ForegroundColor White
Write-Host "3. Test signup: POST /api/Signup" -ForegroundColor White
Write-Host "4. Create workflows and publish them" -ForegroundColor White
Write-Host ""
Write-Host "Logs saved to:" -ForegroundColor Yellow
Write-Host "  - catalog_setup.log" -ForegroundColor White
Write-Host "  - tenant_setup.log" -ForegroundColor White
Write-Host ""
Write-Host "Documentation: Read 00_MASTER_SETUP_README.md" -ForegroundColor Cyan
