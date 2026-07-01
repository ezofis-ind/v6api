-- Add StageType to workflow.WorkflowSteps (idempotent)
IF OBJECT_ID(N'workflow.WorkflowSteps', N'U') IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'workflow.WorkflowSteps') AND name = N'StageType')
BEGIN
    ALTER TABLE workflow.WorkflowSteps ADD StageType NVARCHAR(64) NULL;
    PRINT 'Added workflow.WorkflowSteps.StageType';
END
ELSE
    PRINT 'workflow.WorkflowSteps.StageType already exists or table missing';
GO
