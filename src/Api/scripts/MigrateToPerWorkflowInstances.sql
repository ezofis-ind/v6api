-- Migrate existing WorkflowInstances (shared) to per-workflow tables
-- Run on tenant DBs that have data in workflow.WorkflowInstances before switching to per-workflow.
-- Prerequisite: WorkflowInstanceLookup must exist (from CreateWorkflowSchemaComplete.sql)
--
-- For NEW tenants: Signup creates WorkflowInstanceLookup only. Per-workflow tables are created on publish.
-- For EXISTING tenants with shared data: Run this script, or run fresh Test-E2EWorkflow.ps1 with new signup.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstanceLookup' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    RAISERROR('WorkflowInstanceLookup not found. Run CreateWorkflowSchemaComplete.sql first.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstances' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    PRINT 'No shared WorkflowInstances - migration not needed.';
    RETURN;
END

-- Migration requires dynamic SQL per workflow. Use ApplyWorkflowSchemaToTenant.ps1 which runs
-- CreateWorkflowSchemaComplete.sql (adds WorkflowInstanceLookup). For tenants with existing
-- WorkflowInstances data, either:
-- 1) Run Test-E2EWorkflow.ps1 with new signup (fresh tenant, no migration needed)
-- 2) Manually copy rows from WorkflowInstances to WorkflowInstances_{suffix} for each workflow,
--    then insert into WorkflowInstanceLookup. Suffix = first 8 chars of WorkflowId (no hyphens).

PRINT 'WorkflowInstanceLookup exists. New instances will use per-workflow tables.';
PRINT 'For existing data migration, see docs or run fresh signup.';
