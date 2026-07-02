-- Migrate dbo.connector.id from legacy INT IDENTITY to UNIQUEIDENTIFIER (SaaS API).
-- Run on tenant database. Skip if id is already UNIQUEIDENTIFIER.
-- If other tables have FK to connector.id (e.g. connectorHub.connectorId), update those first.

IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'connector'
      AND COLUMN_NAME = 'id' AND DATA_TYPE IN ('int', 'bigint', 'smallint'))
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.tables pt ON fkc.referenced_object_id = pt.object_id
        INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
        WHERE pt.name = 'connector' AND rc.name = 'id' AND OBJECT_NAME(fk.parent_object_id) <> 'connector')
    BEGIN
        RAISERROR('connector.id is referenced by foreign keys. Update dependent tables before running this script.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'connector' AND COLUMN_NAME = 'id_new')
    BEGIN
        ALTER TABLE dbo.connector ADD id_new UNIQUEIDENTIFIER NULL;
        UPDATE dbo.connector SET id_new = NEWID() WHERE id_new IS NULL;

        DECLARE @pk SYSNAME;
        SELECT @pk = kc.name
        FROM sys.key_constraints kc
        INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
        WHERE t.schema_id = SCHEMA_ID('dbo') AND t.name = 'connector' AND kc.type = 'PK';

        IF @pk IS NOT NULL
            EXEC(N'ALTER TABLE dbo.connector DROP CONSTRAINT ' + QUOTENAME(@pk));

        ALTER TABLE dbo.connector DROP COLUMN id;
        EXEC sp_rename N'dbo.connector.id_new', N'id', N'COLUMN';
        ALTER TABLE dbo.connector ALTER COLUMN id UNIQUEIDENTIFIER NOT NULL;
        ALTER TABLE dbo.connector ADD CONSTRAINT PK_connector PRIMARY KEY (id);
    END
END
