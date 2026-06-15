# Database Scripts Index

## Quick Start

See **00_SETUP_README.md** for full setup and E2E test instructions.

## Scripts

| Script | Purpose |
|--------|---------|
| **RunCatalogScripts.ps1** | One-click catalog setup (DB + tables + Hangfire) |
| **RunTenantSchema.ps1** | Apply workflow schema to tenant DB |
| **Test-E2EWorkflow.ps1** | E2E test: Invoice Approval (5 steps) – Signup → Login → Create → Publish → Start → Approve/Reject |
| **Test-E2EMultiUserApproval.ps1** | E2E test: AnyOneApprove + AllMustApprove approval policies |
| **Test-E2EAllWorkflows.ps1** | Runs both workflow tests (Invoice Approval + Multi-User Approval) |
| **Test-E2EDms.ps1** | E2E test: DMS/Repository – Signup → Login → Folder tree → Documents |
| **ResetCatalog.ps1** | Drop and recreate catalog DB for fresh start (tenant DBs not dropped) |
| **Apply-SchemaUpdates.ps1** | Add IsSuperuser to catalog, update existing deployments |
| **01a_CreateCatalogDatabase.sql** | Create catalog database only |
| **01b_CreateCatalogTables.sql** | Create Tenants, UserTenants in catalog schema |
| **01c_InstallHangfire.sql** | Install Hangfire schema in catalog |
| **CreateWorkflowSchemaComplete.sql** | Workflow schema for tenant DB (workflow.* tables) |
| **CreateDmsSchema.sql** | DMS schema for tenant DB (dms.Repository, StagingItems, sample_items, etc.) |
| **AddDmsStagingItems.sql** | Add dms.StagingItems to existing tenant DBs (temp indexing before export) |

## Setup Order

1. **Catalog:** `.\scripts\RunCatalogScripts.ps1` (once)
2. **Tenant:** Via Signup API (auto creates Workflow + DMS schema) or `.\scripts\02_CreateTenantDatabase.sql` (manual)
3. **Demo:** `.\scripts\Test-E2EWorkflow.ps1` or `.\scripts\Test-E2EAllWorkflows.ps1` or `.\scripts\Test-E2EDms.ps1` (API must be running)

## Catalog Reset

To remove and recreate catalog: `.\scripts\ResetCatalog.ps1`. Then run `01b` and `01c` (or use RunCatalogScripts.ps1). Tenant DBs are not dropped.
