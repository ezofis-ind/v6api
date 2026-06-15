-- Optional: remove Review from workflow.WorkflowSteps (review belongs on transaction move, not step definition)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('workflow.WorkflowSteps') AND name = 'Review')
BEGIN
    ALTER TABLE workflow.WorkflowSteps DROP COLUMN Review;
    PRINT 'Dropped workflow.WorkflowSteps.Review';
END
GO
