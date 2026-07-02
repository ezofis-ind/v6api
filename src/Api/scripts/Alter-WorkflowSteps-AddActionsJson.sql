-- Add ActionsJson to workflow.WorkflowSteps (outgoing Rules per activity from designer JSON).
-- Run against each tenant database.

IF OBJECT_ID(N'workflow.WorkflowSteps', N'U') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'workflow.WorkflowSteps') AND name = N'ActionsJson')
BEGIN
    ALTER TABLE workflow.WorkflowSteps ADD ActionsJson NVARCHAR(MAX) NULL;
    PRINT 'Added ActionsJson to workflow.WorkflowSteps';
END
ELSE
    PRINT 'ActionsJson already exists on workflow.WorkflowSteps (or table missing).';

-- Example ActionsJson for AP AGENT block DR97uPaylMtwahvi3XYr_:
-- [{"Id":"5EOS4AO4HIdvn1aCipx79","ProceedAction":"APPROVED","ToBlockId":"so19PaUUTXJsN9kBXb3N6"},
--  {"Id":"Ky1L1OSEi6bfegdh3xYNA","ProceedAction":"REJECTED","ToBlockId":"tGLZHXsPrkiaMWWWm4hhQ"},
--  {"Id":"tjwZoRiRJIt4132e_a61u","ProceedAction":"PARTIALLY APPROVED","ToBlockId":"zigR-RzJQPjLv3ckgndxU"}]
--
-- Re-sync steps from designer JSON via PUT /api/workflows/{id} or POST sync-steps to populate.
