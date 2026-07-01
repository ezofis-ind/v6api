# Database Setup Guide

## Quick Table Check

```powershell
# List all tables in catalog
.\scripts\Verify-Tables.ps1

# List tables in catalog + tenant
.\scripts\Verify-Tables.ps1 -TenantId "your-tenant-guid"
```

## Key Tables

| Database | Schema | Key Tables |
|----------|--------|------------|
| **Catalog** | catalog | Tenants, UserTenants |
| **Tenant** | workflow | Workflows, WorkflowSteps, WorkflowInstances, WorkflowStepInstances |
| **Tenant** | users | Users |

## Overview

| Script | Purpose | When to Run |
|--------|---------|-------------|
| **RunCatalogScripts.ps1** | Catalog DB: Tenants, UserTenants, Hangfire | Once per environment |
| **RunTenantSchema.ps1** / **ApplyWorkflowSchemaToTenant.ps1** | Tenant DB: workflow schema + tables | Per tenant (or auto via Signup) |
| **Test-E2EWorkflow.ps1** | E2E test: Invoice Approval (5 steps) – Signup → Create → Publish → Start → Approve/Reject | Demo/delivery |

## 1. Catalog Setup (run once)

```powershell
cd c:\ezofis\Project\Cursor\v6\ezSaaSApi
.\scripts\RunCatalogScripts.ps1
```

Or with custom server:
```powershell
.\scripts\RunCatalogScripts.ps1 -Server "yourserver.database.windows.net" -User "user" -Password "pass" -Database "ezofis_catalog_Dev"
```

**What it does:** Creates catalog database, `catalog.Tenants`, `catalog.UserTenants`, Hangfire tables.

## 2. Tenant Setup

**Option A – Via API Signup (recommended)**  
Signup creates the tenant DB and applies workflow schema automatically:

```powershell
.\scripts\Test-E2EWorkflow.ps1
```

**Option B – Manual (existing tenant missing workflow schema)**  
If a tenant was created before workflow schema existed:

```powershell
.\scripts\RunTenantSchema.ps1 -TenantId "your-tenant-guid"
# Or with database name:
.\scripts\RunTenantSchema.ps1 -TenantDatabase "ezofis_Tenant_1"
```

## 3. Run API

```powershell
dotnet run --project src/Api
```

Swagger: https://localhost:5001/swagger

## 4. E2E Test Run (Demo)

```powershell
# Start API first (in another terminal):
dotnet run --project src/Api

# Run full test: Signup → Login → Create Workflow → Publish → Start Flow
.\scripts\Test-E2EWorkflow.ps1
```

With existing tenant:
```powershell
.\scripts\Test-E2EWorkflow.ps1 -SkipSignup -TenantId "your-tenant-guid"
```
