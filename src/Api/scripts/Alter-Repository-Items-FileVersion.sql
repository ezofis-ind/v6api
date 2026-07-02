-- Add FileVersion to all per-repository items tables (repository.Items_xxxxxxxx).
-- Safe to re-run. First upload = 1; same FolderId + FileName = 2, 3, ...

SET NOCOUNT ON;

DECLARE @TableName SYSNAME;
DECLARE @Sql NVARCHAR(MAX);

DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'repository'
      AND t.name LIKE N'Items[_]%'
      AND t.name NOT LIKE N'%History'
      AND t.name NOT LIKE N'%Stage';

OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'
IF COL_LENGTH(N''repository.' + @TableName + N''', N''FileVersion'') IS NULL
BEGIN
    ALTER TABLE repository.[' + @TableName + N']
        ADD [FileVersion] INT NOT NULL CONSTRAINT [DF_' + @TableName + N'_FileVersion] DEFAULT (1);
    PRINT N''Added FileVersion to repository.[' + @TableName + N']'';
END';

    EXEC sp_executesql @Sql;

    FETCH NEXT FROM table_cursor INTO @TableName;
END

CLOSE table_cursor;
DEALLOCATE table_cursor;

PRINT N'Done.';
