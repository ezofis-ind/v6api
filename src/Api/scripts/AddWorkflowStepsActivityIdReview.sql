-- Add ActivityId to workflow.WorkflowSteps (idempotent)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('workflow.WorkflowSteps') AND name = 'ActivityId')
BEGIN
    ALTER TABLE workflow.WorkflowSteps ADD ActivityId NVARCHAR(128) NULL;
    PRINT 'Added workflow.WorkflowSteps.ActivityId';
END
GO
