-- Optional: self-referencing FK for repository.Folders.ParentId
-- Run on tenant database if repository.Folders already exists without FK.

IF EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'repository' AND t.name = N'Folders')
AND NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Folders_Parent' AND parent_object_id = OBJECT_ID(N'repository.Folders'))
BEGIN
    ALTER TABLE repository.Folders
    ADD CONSTRAINT FK_Folders_Parent
        FOREIGN KEY (ParentId) REFERENCES repository.Folders (Id);
    PRINT 'FK_Folders_Parent added.';
END
GO
