-- Add RepositoryId and FormId to workflow.Workflows (run on tenant DB).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'workflow')
    EXEC('CREATE SCHEMA workflow');
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('workflow.Workflows') AND name = 'RepositoryId')
BEGIN
    ALTER TABLE workflow.Workflows ADD RepositoryId NVARCHAR(64) NULL;
    PRINT 'Added workflow.Workflows.RepositoryId';
END
ELSE IF EXISTS (
    SELECT 1 FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('workflow.Workflows') AND c.name = 'RepositoryId' AND t.name = 'uniqueidentifier')
BEGIN
    ALTER TABLE workflow.Workflows ALTER COLUMN RepositoryId NVARCHAR(64) NULL;
    PRINT 'Altered workflow.Workflows.RepositoryId to NVARCHAR(64)';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('workflow.Workflows') AND name = 'FormId')
BEGIN
    ALTER TABLE workflow.Workflows ADD FormId NVARCHAR(64) NULL;
    PRINT 'Added workflow.Workflows.FormId';
END
GO
