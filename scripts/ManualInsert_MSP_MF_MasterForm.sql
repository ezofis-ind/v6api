/*
  Manual insert: MSP_MF master form (legacy v5 → SaaS GUID format)

  Generated form GUID : 7f8a9b0c-1d2e-4f3a-9b5c-6d7e8f9a0b1c
  ezfb items table    : dbo.ezfb_7f8a9b0c_items   (first 8 hex chars of GUID, lowercase)

  BEFORE RUNNING — set these variables for your tenant DB:
    @TenantIntId   → SELECT TenantIntId FROM dbo.TenantIdMap WHERE TenantGuid = '<your-tenant-guid>'
    @CreatedBy     → admin user GUID as string, e.g. 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'
    @RepositoryId   → legacy INT repo id if wForm.repositoryId is INT; else NULL

  Run on the TENANT database (not catalog).
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ========== CONFIGURE ==========
DECLARE @FormId        NVARCHAR(64)  = N'7f8a9b0c-1d2e-4f3a-9b5c-6d7e8f9a0b1c';
DECLARE @TenantIntId   INT           = 1;          -- change me
DECLARE @CreatedBy     NVARCHAR(50)  = N'00000000-0000-0000-0000-000000000001'; -- change me
DECLARE @RepositoryId  INT           = NULL;       -- legacy was 36; set if you have INT mapping
DECLARE @Now           NVARCHAR(50)  = CONVERT(NVARCHAR(50), SYSUTCDATETIME(), 127);

DECLARE @PODetailControlId INT;

BEGIN TRANSACTION;

-- ---------------------------------------------------------------------------
-- 1) dbo.wForm
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.wForm WHERE id = @FormId)
BEGIN
    INSERT INTO dbo.wForm (
        id, uid, tenantId, name, description, type, layout, publishOption, error,
        createdAt, modifiedAt, createdBy, modifiedBy, isDeleted, qrFields, isEdit,
        repositoryId, uniqueColumns, superUser, entryUser, activityBy, activityOn, activityId
    )
    VALUES (
        @FormId,
        N'91XiXcI-vytg6NaSBJQWo',          -- legacy uid (optional traceability)
        @TenantIntId,
        N'MSP_MF',
        N'',
        N'MASTER',
        N'CLASSIC',
        N'PUBLISHED',
        N'',
        @Now,
        @Now,
        @CreatedBy,
        @CreatedBy,
        0,
        N'',
        0,
        @RepositoryId,
        N'pLIax1zKPXRCdlnCOLHzy',          -- PO Number = unique column
        N'',
        N'',
        NULL,
        NULL,
        NULL
    );
    PRINT 'Inserted wForm: ' + @FormId;
END
ELSE
    PRINT 'wForm already exists: ' + @FormId;

-- ---------------------------------------------------------------------------
-- 2) dbo.wFormControl  (parentId 0 = top-level; TABLE children use @PODetailControlId)
-- ---------------------------------------------------------------------------
DELETE FROM dbo.wFormControl WHERE wFormId = @FormId;

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'pLIax1zKPXRCdlnCOLHzy', N'PO Number',       N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'IChfsjDJrJsfQDRVM9EAD', N'Vendor',          N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'szG2cMaKkEtkY-V8dNQQU', N'Vendor Name',     N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'l7GRxwpdjcWv8LSz2BlmV', N'Bill-To Address', N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'cpyUkkJaPgvNbUQCb_z6e', N'Ship-To Address', N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'Cghd2DGqBZpvfmqwFmd8z', N'PO Date',         N'DATE',       0, 0, @Now, @CreatedBy, 0, NULL);

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'bnWTWmxoqfafNDOrwqYVT', N'Terms Descr',     N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'Adcwz0bdXvk6dvSCeN9xB', N'Buyer Name',      N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'K7jbH86jW3DOVC9ElLn4z', N'Gross Amount',    N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'A6WOvkJ7iVzFiSp6uftqC', N'Notes',           N'SHORT_TEXT', 0, 0, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES (@FormId, N'8G9Iy7Dtj6ksL1ikYVmEp', N'PODetail',        N'TABLE',      0, 0, @Now, @CreatedBy, 0, NULL);
SET @PODetailControlId = SCOPE_IDENTITY();

INSERT INTO dbo.wFormControl (wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson)
VALUES
    (@FormId, N'EWdBSDlm185GKpbReyvAx', N'Line',             N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'hhV29UwNDr85bFxTpw0pK', N'Part Number',      N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'65FcXXq2C4JG5lFZWGwu-', N'Rev',              N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'Gx1FM5LN2nct-_fLeLn63', N'Part Description', N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'X_K-QEAdydBLh08Pf4FZY', N'UOM',              N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'Fk4UvCUeIWcHJxTxGSQi9', N'Tax',              N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'hnzgiQph-MXpYA4bowCiI', N'Quantity',         N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'm0XNzT9VELdAFFKfXZAYa', N'Unit Cost',        N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'4hhwce0cB1NA_CJJyF5Y2', N'Gross Amount1',    N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'y-8t3wTX5XUkrbbE3ZHpi', N'Req Date',         N'DATE',       0, @PODetailControlId, @Now, @CreatedBy, 0, NULL),
    (@FormId, N'rJemAZd-lVI-rRjTqTe05', N'Weight',           N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}'),
    (@FormId, N'W6qlf2mHG_WyKim70kowj', N'G/L Account',      N'SHORT_TEXT', 0, @PODetailControlId, @Now, @CreatedBy, 0, N'{"specific":{},"validation":{"contentRule":"TEXT","maximum":"","minimum":""}}');

PRINT 'Inserted wFormControl rows for form ' + @FormId;

-- ---------------------------------------------------------------------------
-- 3) dbo.ezfb_7f8a9b0c_items
--
--    wFormControl has 23 rows but ezfb needs only 11 DATA columns:
--      - 11 top-level controls (parentId = 0), including PODetail TABLE
--      - 12 PODetail child controls live INSIDE [8G9Iy7Dtj6ksL1ikYVmEp] as JSON
--
--    Column names = exact legacy jsonId (hyphens kept). SaaS API also resolves
--    sanitized names (hyphens stripped) via TryResolveEzfbColumn.
--
--    If workflow bootstrap created a minimal ezfb table first (no field cols),
--    the ALTER block below adds any missing columns.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'dbo' AND t.name = N'ezfb_7f8a9b0c_items'
)
BEGIN
    CREATE TABLE dbo.ezfb_7f8a9b0c_items (
        itemId      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [pLIax1zKPXRCdlnCOLHzy] NVARCHAR(MAX) NULL,
        [IChfsjDJrJsfQDRVM9EAD] NVARCHAR(MAX) NULL,
        [szG2cMaKkEtkY-V8dNQQU]  NVARCHAR(MAX) NULL,
        [l7GRxwpdjcWv8LSz2BlmV]   NVARCHAR(MAX) NULL,
        [cpyUkkJaPgvNbUQCb_z6e]   NVARCHAR(MAX) NULL,
        [Cghd2DGqBZpvfmqwFmd8z]   NVARCHAR(MAX) NULL,
        [bnWTWmxoqfafNDOrwqYVT]   NVARCHAR(MAX) NULL,
        [Adcwz0bdXvk6dvSCeN9xB]   NVARCHAR(MAX) NULL,
        [K7jbH86jW3DOVC9ElLn4z]   NVARCHAR(MAX) NULL,
        [A6WOvkJ7iVzFiSp6uftqC]   NVARCHAR(MAX) NULL,
        [8G9Iy7Dtj6ksL1ikYVmEp]   NVARCHAR(MAX) NULL,   -- PODetail JSON array
        createdAt   NVARCHAR(50) NULL,
        modifiedAt  NVARCHAR(50) NULL,
        createdBy   NVARCHAR(50) NOT NULL CONSTRAINT DF_ezfb_7f8a9b0c_createdBy DEFAULT(N'0'),
        modifiedBy  NVARCHAR(50) NOT NULL CONSTRAINT DF_ezfb_7f8a9b0c_modifiedBy DEFAULT(N'0'),
        isDeleted   BIT NOT NULL CONSTRAINT DF_ezfb_7f8a9b0c_isDeleted DEFAULT(0),
        todayTask   BIT NOT NULL CONSTRAINT DF_ezfb_7f8a9b0c_todayTask DEFAULT(1),
        isMarked    BIT NOT NULL CONSTRAINT DF_ezfb_7f8a9b0c_isMarked DEFAULT(0),
        ValidFrom   DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN
                    CONSTRAINT DF_ezfb_7f8a9b0c_ValidFrom DEFAULT SYSUTCDATETIME(),
        ValidTo     DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN
                    CONSTRAINT DF_ezfb_7f8a9b0c_ValidTo DEFAULT CONVERT(DATETIME2, '9999-12-31 23:59:59.9999999'),
        PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)
    )
    WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.ezfb_7f8a9b0c_history));

    PRINT 'Created dbo.ezfb_7f8a9b0c_items';
END
ELSE
    PRINT 'Table dbo.ezfb_7f8a9b0c_items already exists — ensuring missing columns';

-- Ensure all 11 top-level field columns exist (safe to re-run)
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'pLIax1zKPXRCdlnCOLHzy') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [pLIax1zKPXRCdlnCOLHzy] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'IChfsjDJrJsfQDRVM9EAD') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [IChfsjDJrJsfQDRVM9EAD] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'szG2cMaKkEtkY-V8dNQQU') IS NULL
BEGIN
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [szG2cMaKkEtkY-V8dNQQU] NVARCHAR(MAX) NULL;
    -- Copy from SaaS-sanitized column if API created table without hyphen
    IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'szG2cMaKkEtkYV8dNQQU') IS NOT NULL
        EXEC(N'UPDATE dbo.ezfb_7f8a9b0c_items SET [szG2cMaKkEtkY-V8dNQQU] = [szG2cMaKkEtkYV8dNQQU] WHERE [szG2cMaKkEtkY-V8dNQQU] IS NULL AND [szG2cMaKkEtkYV8dNQQU] IS NOT NULL');
END
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'l7GRxwpdjcWv8LSz2BlmV') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [l7GRxwpdjcWv8LSz2BlmV] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'cpyUkkJaPgvNbUQCb_z6e') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [cpyUkkJaPgvNbUQCb_z6e] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'Cghd2DGqBZpvfmqwFmd8z') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [Cghd2DGqBZpvfmqwFmd8z] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'bnWTWmxoqfafNDOrwqYVT') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [bnWTWmxoqfafNDOrwqYVT] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'Adcwz0bdXvk6dvSCeN9xB') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [Adcwz0bdXvk6dvSCeN9xB] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'K7jbH86jW3DOVC9ElLn4z') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [K7jbH86jW3DOVC9ElLn4z] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'A6WOvkJ7iVzFiSp6uftqC') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [A6WOvkJ7iVzFiSp6uftqC] NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'8G9Iy7Dtj6ksL1ikYVmEp') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD [8G9Iy7Dtj6ksL1ikYVmEp] NVARCHAR(MAX) NULL;

-- Metadata columns (minimal bootstrap tables often omit these)
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'createdAt') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD createdAt NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'modifiedAt') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD modifiedAt NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'createdBy') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD createdBy NVARCHAR(50) NOT NULL
        CONSTRAINT DF_ezfb_7f8a9b0c_createdBy DEFAULT(N'0');
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'modifiedBy') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD modifiedBy NVARCHAR(50) NOT NULL
        CONSTRAINT DF_ezfb_7f8a9b0c_modifiedBy DEFAULT(N'0');
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'isDeleted') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD isDeleted BIT NOT NULL
        CONSTRAINT DF_ezfb_7f8a9b0c_isDeleted DEFAULT(0);
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'todayTask') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD todayTask BIT NOT NULL
        CONSTRAINT DF_ezfb_7f8a9b0c_todayTask DEFAULT(1);
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'isMarked') IS NULL
    ALTER TABLE dbo.ezfb_7f8a9b0c_items ADD isMarked BIT NOT NULL
        CONSTRAINT DF_ezfb_7f8a9b0c_isMarked DEFAULT(0);

-- Normalize legacy PascalCase ItemId → itemId if needed
IF COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'itemId') IS NULL
   AND COL_LENGTH(N'dbo.ezfb_7f8a9b0c_items', N'ItemId') IS NOT NULL
    EXEC sp_rename N'dbo.ezfb_7f8a9b0c_items.ItemId', N'itemId', N'COLUMN';

PRINT 'ezfb column check complete (expect 11 field cols + metadata, not 23).';

-- ---------------------------------------------------------------------------
-- 4) Item rows — run scripts/Insert_MSP_MF_ezfb_items.sql after this script
--    (244 legacy rows; preserves itemId via IDENTITY_INSERT)
-- ---------------------------------------------------------------------------
PRINT 'Next step: run Insert_MSP_MF_ezfb_items.sql on this database.';

COMMIT TRANSACTION;

-- ========== REFERENCE ==========
SELECT id AS FormGuid, name, type, publishOption, uniqueColumns FROM dbo.wForm WHERE id = @FormId;
SELECT COUNT(*) AS wFormControlCount FROM dbo.wFormControl WHERE wFormId = @FormId;
SELECT COUNT(*) AS ezfbFieldColumnCount
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'ezfb_7f8a9b0c_items'
  AND COLUMN_NAME IN (
    N'pLIax1zKPXRCdlnCOLHzy', N'IChfsjDJrJsfQDRVM9EAD', N'szG2cMaKkEtkY-V8dNQQU',
    N'l7GRxwpdjcWv8LSz2BlmV', N'cpyUkkJaPgvNbUQCb_z6e', N'Cghd2DGqBZpvfmqwFmd8z',
    N'bnWTWmxoqfafNDOrwqYVT', N'Adcwz0bdXvk6dvSCeN9xB', N'K7jbH86jW3DOVC9ElLn4z',
    N'A6WOvkJ7iVzFiSp6uftqC', N'8G9Iy7Dtj6ksL1ikYVmEp'
  );
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'ezfb_7f8a9b0c_items'
ORDER BY ORDINAL_POSITION;
SELECT TOP 5 itemId, [pLIax1zKPXRCdlnCOLHzy] AS PONumber FROM dbo.ezfb_7f8a9b0c_items;

PRINT '';
PRINT 'Use this FormId in workflows / API: 7f8a9b0c-1d2e-4f3a-9b5c-6d7e8f9a0b1c';
PRINT 'ezfb table name: ezfb_7f8a9b0c_items';
