# Master Database Setup Scripts

## 📋 Complete Setup Guide

This folder contains **consolidated scripts** that set up everything you need. No more missing tables or partial setups!

## 🎯 Setup Order

### Step 1: Catalog Database (Run Once)
**Scripts:** `01a_CreateCatalogDatabase.sql` + `01b_CreateCatalogTables.sql`

**What it creates:**
- ✅ Catalog database: `ezofis_catalog_Dev`
- ✅ `Tenants` table
- ✅ `UserTenants` table
- ✅ EF Migrations history

**How to run (three steps required for Azure SQL):**
```bash
# Step 1: Create database
sqlcmd -S localhost -d master -E -i scripts/01a_CreateCatalogDatabase.sql

# Step 2: Create tables
sqlcmd -S localhost -d ezofis_catalog_Dev -E -i scripts/01b_CreateCatalogTables.sql

# Step 3: Install Hangfire (background jobs)
sqlcmd -S localhost -d ezofis_catalog_Dev -E -i scripts/01c_InstallHangfire.sql
```

**SSMS/Azure Data Studio:** Run `01a` against master, then `01b` and `01c` against ezofis_catalog_Dev.

### Step 2: Tenant Database (Run for Each Tenant)
**Script:** `02_CreateTenantDatabase.sql`

**What it creates:**
- ✅ Tenant database: `ezofis_Tenant_X`
- ✅ **Users schema** (1 table):
  - `users.Users` (with all extended profile fields)
- ✅ **Workflow schema** (7 core tables):
  - `workflow.Workflows`
  - `workflow.WorkflowSteps`
  - `workflow.WorkflowInstances` (with temporal history)
  - `workflow.WorkflowStepInstances`
  - `workflow.WorkflowApprovals`
  - `workflow.WorkflowSlas`
  - `workflow.WorkflowInstanceSlas`
- ✅ Temporal tables for audit history
- ✅ EF Migrations history for both schemas

**IMPORTANT:** Edit the script and change these lines:
```sql
-- Line 8: Change database name
DECLARE @TenantDatabaseName NVARCHAR(128) = 'ezofis_Tenant_1';  -- CHANGE THIS!

-- Line 36: Change database name
USE [ezofis_Tenant_1]  -- CHANGE THIS!
```

**How to run:**
```bash
# Option 1: Command line
sqlcmd -S localhost -i scripts/02_CreateTenantDatabase.sql

# Option 2: SSMS/Azure Data Studio
# Open 02_CreateTenantDatabase.sql
# Edit the database name (lines 8 and 36)
# Execute
```

## 🔄 Complete Fresh Setup

If you want to start completely fresh (e.g., after deleting old databases):

```bash
# 1. Create catalog database
sqlcmd -S localhost -d master -E -i scripts/01a_CreateCatalogDatabase.sql
sqlcmd -S localhost -d ezofis_catalog_Dev -E -i scripts/01b_CreateCatalogTables.sql
sqlcmd -S localhost -d ezofis_catalog_Dev -E -i scripts/01c_InstallHangfire.sql

# 2. Create first tenant database
# Edit 02_CreateTenantDatabase.sql first (change database name)
sqlcmd -S localhost -E -i scripts/02_CreateTenantDatabase.sql

# 3. Register tenant in catalog
sqlcmd -S localhost -d ezofis_catalog_Dev -Q "
INSERT INTO Tenants (Id, Name, ConnectionString, IsActive, CreatedAtUtc)
VALUES (
  NEWID(), 
  'Test Corp',
  'Data Source=localhost;Initial Catalog=ezofis_Tenant_1;Integrated Security=true;',
  1,
  SYSUTCDATETIME()
)
SELECT * FROM Tenants
"
```

## 🚀 Using the API (Recommended)

Instead of running SQL scripts manually, you can use the **Signup API** which does everything automatically:

```bash
# 1. Start API
cd src/Api
dotnet run

# 2. Sign up via Swagger (https://localhost:5001/swagger)
POST /api/Signup
{
  "organizationName": "Test Corp",
  "email": "admin@test.com",
  "password": "Test@123",
  "firstName": "John",
  "lastName": "Doe"
}
```

**This automatically:**
1. ✅ Creates tenant database
2. ✅ Creates Users schema (via EF migrations)
3. ✅ Creates Workflow schema (via SQL script)
4. ✅ Registers tenant in catalog
5. ✅ Creates admin user
6. ✅ Registers user in UserTenants

## 📊 What Gets Created

### Catalog Database (`ezofis_catalog_Dev`)
```
dbo.Tenants                    - Tenant registry
dbo.UserTenants                - User-to-tenant mapping
dbo.__EFMigrationsHistory      - Migration tracking
HangFire.*                     - Hangfire background jobs (Job, State, JobQueue, etc.)
```

### Tenant Database (`ezofis_Tenant_X`)
```
users.Users                           - User accounts
users.__EFMigrationsHistory           - Users migration tracking

workflow.Workflows                    - Workflow definitions
workflow.WorkflowSteps                - Workflow steps
workflow.WorkflowInstances            - Workflow executions (TEMPORAL)
workflow.WorkflowInstancesHistory     - Audit history (auto-created)
workflow.WorkflowStepInstances        - Step executions
workflow.WorkflowApprovals            - Approval requests
workflow.WorkflowSlas                 - SLA policies
workflow.WorkflowInstanceSlas         - SLA tracking
workflow.__EFMigrationsHistory        - Workflow migration tracking
```

### Dynamic Tables (Created on Workflow Publish)
```
For each published workflow (ID: abc12345-...):

workflow.WorkflowComments_abc12345
workflow.WorkflowAttachments_abc12345
workflow.WorkflowForms_abc12345
workflow.WorkflowTasks_abc12345
workflow.WorkflowSignatures_abc12345
workflow.WorkflowDocuments_abc12345
workflow.WorkflowEmails_abc12345
workflow.WorkflowAiValidations_abc12345
workflow.WorkflowPdfAnnotations_abc12345
```

## 📝 Script Summary

| Script | Purpose |
|--------|---------|
| `01a_CreateCatalogDatabase.sql` | Create catalog database |
| `01b_CreateCatalogTables.sql` | Create Tenants, UserTenants tables |
| `01c_InstallHangfire.sql` | Install Hangfire schema (background jobs) |
| `02_CreateTenantDatabase.sql` | Create tenant DB + Users + Workflow schemas |
| `CreateWorkflowSchemaComplete.sql` | Workflow schema (used by API during tenant signup) |

## ⚠️ Important Notes

### Database Names
- **Catalog:** `ezofis_catalog_Dev` (fixed name)
- **Tenants:** `ezofis_Tenant_1`, `ezofis_Tenant_2`, etc. (sequential)

### Connection Strings
Update these in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=localhost;Initial Catalog=ezofis_catalog_Dev;Integrated Security=true;"
  }
}
```

### Temporal Tables
- Requires **SQL Server 2016+** or **Azure SQL Database**
- Automatically tracks all changes to `WorkflowInstances`
- Query history: `SELECT * FROM workflow.WorkflowInstances FOR SYSTEM_TIME ALL`

### Dynamic Tables
- Created **automatically** when you publish a workflow
- Naming: `TableName_<first8chars>` (e.g., `WorkflowComments_abc12345`)
- One set of 9 tables per published workflow

## 🎯 Recommended Workflow

### For Development
```bash
# 1. Use API signup (easiest)
POST /api/Signup

# 2. If signup fails, run manual scripts
sqlcmd -S localhost -d master -E -i scripts/01a_CreateCatalogDatabase.sql
sqlcmd -S localhost -d ezofis_catalog_Dev -E -i scripts/01b_CreateCatalogTables.sql
sqlcmd -S localhost -d ezofis_catalog_Dev -E -i scripts/01c_InstallHangfire.sql
sqlcmd -S localhost -E -i scripts/02_CreateTenantDatabase.sql
```

### For Complete Reset
```bash
# 1. Drop databases manually in SSMS (or delete via SQL)
# 2. Rebuild from scratch
sqlcmd -S localhost -d master -E -i scripts/01a_CreateCatalogDatabase.sql
sqlcmd -S localhost -d ezofis_catalog_Dev -E -i scripts/01b_CreateCatalogTables.sql
sqlcmd -S localhost -d ezofis_catalog_Dev -E -i scripts/01c_InstallHangfire.sql
sqlcmd -S localhost -E -i scripts/02_CreateTenantDatabase.sql
```

## 🆘 Troubleshooting

### "Invalid object name 'workflow.Workflows'"
**Solution:** Workflow tables not created. Run `02_CreateTenantDatabase.sql` on the tenant database, or use API signup which creates them automatically.

### "Workflow schema script not found"
**Solution:** Copy script to output:
```powershell
cd src/Api
New-Item -ItemType Directory -Path "bin\Debug\net8.0\scripts" -Force
Copy-Item "..\..\scripts\CreateWorkflowSchemaComplete.sql" -Destination "bin\Debug\net8.0\scripts\" -Force
```

### Temporal table errors
**Solution:** Ensure SQL Server 2016+ or Azure SQL Database

---

**Last Updated:** 2026-02-26  
**Version:** 1.0 - Consolidated master scripts
