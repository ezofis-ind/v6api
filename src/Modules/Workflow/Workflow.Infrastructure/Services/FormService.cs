using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Forms;
using SaaSApp.Workflow.Application.Workflows;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>v5 newformAsync parity: wForm row, JSON storage, security, published controls and ezfb_{id}_items.</summary>
public sealed partial class FormService : IFormService
{
    private static readonly HashSet<string> SkippedFieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PARAGRAPH", "DIVIDER", "LABEL"
    };

    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFormJsonStorageService _formJsonStorage;
    private readonly ILogger<FormService> _logger;

    public FormService(
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        IFormJsonStorageService formJsonStorage,
        ILogger<FormService> logger)
    {
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _formJsonStorage = formJsonStorage;
        _logger = logger;
    }

    public async Task<FormCreateResult> CreateFormAsync(FormJsonDto formJson, string rawJson, CancellationToken cancellationToken = default)
    {
        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");
        var tenantGuid = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var general = formJson.Settings?.General
            ?? throw new InvalidOperationException("settings.general is required.");
        var name = general.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("settings.general.name is required.");

        var publish = formJson.Settings?.Publish;
        var publishOption = publish?.PublishOption?.Trim() ?? "DRAFT";
        var isPublished = publishOption.Equals("PUBLISHED", StringComparison.OrdinalIgnoreCase);

        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");
        var createdBy = userId.ToString("D");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureFormSchemaAsync(connection, cancellationToken);

        if (await FormNameExistsAsync(connection, tenantGuid, name, cancellationToken))
            return new FormCreateResult(FormCreateStatus.NameConflict, null, "form already Exist");

        var formId = await InsertWFormAsync(
            connection,
            tenantGuid,
            formJson,
            general,
            publishOption,
            createdBy,
            now,
            cancellationToken);

        var jsonToStore = !string.IsNullOrWhiteSpace(rawJson)
            ? rawJson
            : JsonSerializer.Serialize(formJson, WorkflowJsonSerializerOptions.Storage);
        await _formJsonStorage.SaveFormJsonAsync(formId, jsonToStore, cancellationToken);

        var securityUserIds = CollectSecurityUserIds(createdBy, general.SuperUser, general.EntryUser);
        await InsertFormSecurityAsync(connection, formId, securityUserIds, createdBy, now, cancellationToken);

        if (isPublished)
        {
            var panels = formJson.Panels ?? new List<FormPanelDto>();
            var secondaryPanels = formJson.SecondaryPanels ?? new List<FormPanelDto>();
            var fields = CollectEntryFields(panels);

            if (fields.Count == 0)
                return new FormCreateResult(FormCreateStatus.NotFound, formId, "Formfields not found");

            await SyncFormControlsAsync(connection, formId, panels, secondaryPanels, fields, createdBy, now, cancellationToken);
            await EnsureFormEntryTableAsync(connection, formId, fields, cancellationToken);
        }

        _logger.LogInformation("Created form {FormId} ({Name}), published={Published}", formId, name, isPublished);
        return new FormCreateResult(FormCreateStatus.Created, formId, formId);
    }

    private static List<string> CollectSecurityUserIds(string createdBy, string[]? superUser, string[]? entryUser)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { createdBy };
        if (superUser != null)
        {
            foreach (var u in superUser.Where(x => !string.IsNullOrWhiteSpace(x)))
                set.Add(u.Trim());
        }
        if (entryUser != null)
        {
            foreach (var u in entryUser.Where(x => !string.IsNullOrWhiteSpace(x)))
                set.Add(u.Trim());
        }
        return set.ToList();
    }

    private static List<FormFieldDto> CollectEntryFields(List<FormPanelDto> panels)
    {
        var list = new List<FormFieldDto>();
        foreach (var panel in panels)
        {
            if (panel.Fields == null)
                continue;
            foreach (var field in panel.Fields)
            {
                if (field.Type == null || SkippedFieldTypes.Contains(field.Type))
                    continue;
                if (string.IsNullOrWhiteSpace(field.Id))
                    continue;
                list.Add(field);
            }
        }
        return list;
    }

    /// <summary>
    /// Called on every POST /api/form before insert. Creates dbo.wForm (+ related tables) only when missing.
    /// Existing legacy tables (e.g. ezofis_Tenant_7) are left unchanged.
    /// </summary>
    private static async Task EnsureFormSchemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "wForm", cancellationToken))
        {
            const string wFormSql = @"
CREATE TABLE dbo.wForm(
    id NVARCHAR(64) NOT NULL PRIMARY KEY,
    uid NVARCHAR(500) NULL,
    tenantId INT NOT NULL,
    name NVARCHAR(500) NOT NULL,
    description NVARCHAR(2000) NULL,
    type NVARCHAR(100) NULL,
    layout NVARCHAR(500) NULL,
    publishOption NVARCHAR(500) NULL,
    error NVARCHAR(MAX) NULL,
    createdAt NVARCHAR(50) NULL,
    modifiedAt NVARCHAR(50) NULL,
    createdBy NVARCHAR(50) NOT NULL DEFAULT('0'),
    modifiedBy NVARCHAR(50) NOT NULL DEFAULT('0'),
    isDeleted BIT NOT NULL DEFAULT(0),
    qrFields NVARCHAR(MAX) NULL,
    isEdit INT NOT NULL DEFAULT(0),
    repositoryId INT NULL,
    uniqueColumns NVARCHAR(MAX) NULL,
    superUser NVARCHAR(1000) NULL,
    entryUser NVARCHAR(1000) NULL,
    activityBy NVARCHAR(50) NULL,
    activityOn NVARCHAR(50) NULL,
    activityId INT NULL
);
CREATE INDEX IX_wForm_tenantId_name ON dbo.wForm(tenantId, name);";
            await using var cmd = new SqlCommand(wFormSql, connection) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!await TableExistsAsync(connection, "wFormControl", cancellationToken))
        {
            const string controlSql = @"
CREATE TABLE dbo.wFormControl(
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    wFormId NVARCHAR(64) NOT NULL,
    jsonId NVARCHAR(200) NULL,
    name NVARCHAR(1000) NULL,
    type NVARCHAR(200) NULL,
    isMandatory BIT NOT NULL DEFAULT(0),
    parentId INT NOT NULL DEFAULT(0),
    createdAt NVARCHAR(50) NULL,
    modifiedAt NVARCHAR(50) NULL,
    createdBy NVARCHAR(50) NOT NULL DEFAULT('0'),
    modifiedBy NVARCHAR(50) NOT NULL DEFAULT('0'),
    isDeleted BIT NOT NULL DEFAULT(0),
    activityBy NVARCHAR(50) NULL,
    activityOn NVARCHAR(50) NULL,
    activityId INT NULL,
    validationJson NVARCHAR(MAX) NULL
);
CREATE INDEX IX_wFormControl_wFormId ON dbo.wFormControl(wFormId);";
            await using var cmd = new SqlCommand(controlSql, connection) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!await TableExistsAsync(connection, "wFormSecurity", cancellationToken))
        {
            const string securitySql = @"
CREATE TABLE dbo.wFormSecurity(
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    wFormId NVARCHAR(64) NOT NULL,
    userId NVARCHAR(50) NULL,
    userCategory NVARCHAR(500) NULL,
    createdAt NVARCHAR(50) NULL,
    modifiedAt NVARCHAR(50) NULL,
    createdBy NVARCHAR(50) NOT NULL DEFAULT('0'),
    modifiedBy NVARCHAR(50) NOT NULL DEFAULT('0'),
    isDeleted BIT NOT NULL DEFAULT(0)
);
CREATE INDEX IX_wFormSecurity_wFormId ON dbo.wFormSecurity(wFormId);";
            await using var cmd = new SqlCommand(securitySql, connection) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!await TableExistsAsync(connection, "TenantIdMap", cancellationToken))
        {
            const string mapSql = @"
CREATE TABLE dbo.TenantIdMap(
    TenantGuid UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantIntId INT IDENTITY(1,1) NOT NULL UNIQUE,
    CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);";
            await using var cmd = new SqlCommand(mapSql, connection) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await EnsureWFormReferenceIdColumnsNvarcharAsync(connection, cancellationToken);
        await EnsureWFormIdColumnSupportsGuidAsync(connection, cancellationToken);
    }

    /// <summary>dbo.wForm.id must hold full dashed GUID (36 chars, not legacy NVARCHAR(8)).</summary>
    private static async Task EnsureWFormIdColumnSupportsGuidAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "wForm", cancellationToken))
            return;

        var idSqlType = await GetColumnTypeAsync(connection, "wForm", "id", cancellationToken);
        if (IsNumericSqlType(idSqlType))
            throw new InvalidOperationException(
                "dbo.wForm.id is numeric in this tenant. Form create requires wForm.id as NVARCHAR(64) or UNIQUEIDENTIFIER.");

        if (idSqlType == "uniqueidentifier")
            return;

        var maxLen = await GetColumnCharMaxLengthAsync(connection, "wForm", "id", cancellationToken);
        if (maxLen is null or >= 36)
            return;

        var alterSql = "ALTER TABLE dbo.wForm ALTER COLUMN id NVARCHAR(64) NOT NULL;";
        await using var cmd = new SqlCommand(alterSql, connection) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Legacy tenants had wFormControl.wFormId as INT (hex parsed). Align with dbo.wForm.id NVARCHAR (e.g. 6028f9a6).
    /// </summary>
    private static async Task EnsureWFormReferenceIdColumnsNvarcharAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        foreach (var tableName in new[] { "wFormControl", "wFormSecurity" })
        {
            if (!await HasTableAsync(connection, tableName, cancellationToken))
                continue;

            var wFormIdType = await GetColumnTypeAsync(connection, tableName, "wFormId", cancellationToken);
            if (!IsNumericSqlType(wFormIdType))
                continue;

            await AlterWFormIdColumnToNvarcharAsync(connection, tableName, cancellationToken);
        }
    }

    private static async Task AlterWFormIdColumnToNvarcharAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string getIndexesSql = """
            SELECT DISTINCT i.name
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = N'dbo' AND t.name = @TableName AND c.name = N'wFormId'
              AND i.name IS NOT NULL AND i.type > 0;
            """;

        var indexNames = new List<string>();
        await using (var getCmd = new SqlCommand(getIndexesSql, connection))
        {
            getCmd.Parameters.AddWithValue("@TableName", tableName);
            await using var reader = await getCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                indexNames.Add(reader.GetString(0));
        }

        foreach (var indexName in indexNames)
        {
            var dropSql = $"DROP INDEX [{indexName.Replace("]", "]]")}] ON dbo.[{tableName}];";
            await using var dropCmd = new SqlCommand(dropSql, connection) { CommandTimeout = 120 };
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var alterSql = $"ALTER TABLE dbo.[{tableName}] ALTER COLUMN wFormId NVARCHAR(64) NOT NULL;";
        await using (var alterCmd = new SqlCommand(alterSql, connection) { CommandTimeout = 120 })
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);

        var recreateSql = tableName switch
        {
            "wFormControl" =>
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id = t.object_id WHERE t.name = N'wFormControl' AND i.name = N'IX_wFormControl_wFormId') CREATE INDEX IX_wFormControl_wFormId ON dbo.wFormControl(wFormId);",
            "wFormSecurity" =>
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id = t.object_id WHERE t.name = N'wFormSecurity' AND i.name = N'IX_wFormSecurity_wFormId') CREATE INDEX IX_wFormSecurity_wFormId ON dbo.wFormSecurity(wFormId);",
            _ => null
        };

        if (recreateSql != null)
        {
            await using var createCmd = new SqlCommand(recreateSql, connection) { CommandTimeout = 120 };
            await createCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT COUNT(1)
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = 'dbo' AND LOWER(t.name) = LOWER(@TableName)";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<bool> FormNameExistsAsync(
        SqlConnection connection,
        Guid tenantGuid,
        string name,
        CancellationToken cancellationToken)
    {
        var tenantKey = await ResolveTenantKeyAsync(connection, tenantGuid, cancellationToken);
        var tenantType = await GetColumnTypeAsync(connection, "wForm", "tenantId", cancellationToken);

        var sql = tenantType is "int" or "bigint" or "smallint" or "tinyint"
            ? "SELECT COUNT(1) FROM dbo.wForm WHERE isDeleted = 0 AND tenantId = @TenantId AND name = @Name"
            : "SELECT COUNT(1) FROM dbo.wForm WHERE isDeleted = 0 AND tenantId = @TenantId AND name = @Name";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@TenantId", tenantKey);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    private static async Task<string> InsertWFormAsync(
        SqlConnection connection,
        Guid tenantGuid,
        FormJsonDto formJson,
        FormGeneralDto general,
        string publishOption,
        string createdBy,
        string now,
        CancellationToken cancellationToken)
    {
        const string tableName = "wForm";
        var tenantKey = await ResolveTenantKeyAsync(connection, tenantGuid, cancellationToken);
        var idSqlType = await GetColumnTypeAsync(connection, tableName, "id", cancellationToken)
            ?? "nvarchar";
        var idMaxLength = await GetColumnCharMaxLengthAsync(connection, tableName, "id", cancellationToken);
        var formId = await ResolveNewFormIdAsync(connection, formJson, idMaxLength, cancellationToken);

        var qrFields = general.QrFields is { Length: > 0 } ? string.Join(",", general.QrFields) : "";
        var uniqueColumns = general.UniqueColumns is { Length: > 0 } ? string.Join(",", general.UniqueColumns) : "";
        var superUser = general.SuperUser is { Length: > 0 } ? string.Join(",", general.SuperUser) : "";
        var entryUser = general.EntryUser is { Length: > 0 } ? string.Join(",", general.EntryUser) : "";

        var cols = new List<string> { "id", "uid", "tenantId", "name", "description", "type", "layout", "publishOption", "error", "createdAt", "createdBy", "superUser", "entryUser", "qrFields", "uniqueColumns", "isDeleted", "isEdit" };
        var vals = new List<string> { "@Id", "@Uid", "@TenantId", "@Name", "@Description", "@Type", "@Layout", "@PublishOption", "@Error", "@CreatedAt", "@CreatedBy", "@SuperUser", "@EntryUser", "@QrFields", "@UniqueColumns", "0", "0" };

        if (await HasColumnAsync(connection, tableName, "modifiedBy", cancellationToken))
        {
            cols.Add("modifiedBy");
            vals.Add("@CreatedBy");
        }

        if (await IsIdentityColumnAsync(connection, tableName, "id", cancellationToken))
        {
            await using var identityOn = new SqlCommand($"SET IDENTITY_INSERT dbo.[{tableName}] ON", connection);
            await identityOn.ExecuteNonQueryAsync(cancellationToken);
            try
            {
                return await ExecuteWFormInsertAsync(connection, tableName, cols, vals, formId, idSqlType, formJson, general, publishOption, createdBy, now, tenantKey, qrFields, uniqueColumns, superUser, entryUser, cancellationToken);
            }
            finally
            {
                await using var identityOff = new SqlCommand($"SET IDENTITY_INSERT dbo.[{tableName}] OFF", connection);
                await identityOff.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        return await ExecuteWFormInsertAsync(connection, tableName, cols, vals, formId, idSqlType, formJson, general, publishOption, createdBy, now, tenantKey, qrFields, uniqueColumns, superUser, entryUser, cancellationToken);
    }

    private static async Task<string> ExecuteWFormInsertAsync(
        SqlConnection connection,
        string tableName,
        List<string> cols,
        List<string> vals,
        string formId,
        string idSqlType,
        FormJsonDto formJson,
        FormGeneralDto general,
        string publishOption,
        string createdBy,
        string now,
        object tenantKey,
        string qrFields,
        string uniqueColumns,
        string superUser,
        string entryUser,
        CancellationToken cancellationToken)
    {
        var sql = $@"
INSERT INTO dbo.[{tableName}]({string.Join(", ", cols)})
OUTPUT INSERTED.id
VALUES({string.Join(", ", vals)});";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", CoerceIdValue(formId, idSqlType));
        cmd.Parameters.AddWithValue("@Uid", (object?)formJson.Uid ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TenantId", tenantKey);
        cmd.Parameters.AddWithValue("@Name", general.Name!.Trim());
        cmd.Parameters.AddWithValue("@Description", (object?)general.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Type", (object?)general.Type ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Layout", (object?)general.Layout ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PublishOption", publishOption);
        cmd.Parameters.AddWithValue("@Error", "");
        cmd.Parameters.AddWithValue("@CreatedAt", now);
        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
        cmd.Parameters.AddWithValue("@SuperUser", superUser);
        cmd.Parameters.AddWithValue("@EntryUser", entryUser);
        cmd.Parameters.AddWithValue("@QrFields", qrFields);
        cmd.Parameters.AddWithValue("@UniqueColumns", uniqueColumns);

        var idObj = await cmd.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Form insert did not return an id.");
        return Convert.ToString(idObj)?.Trim() ?? formId;
    }

    private static async Task InsertFormSecurityAsync(
        SqlConnection connection,
        string formId,
        List<string> userIds,
        string createdBy,
        string now,
        CancellationToken cancellationToken)
    {
        const string securityTable = "wFormSecurity";
        if (!await HasTableAsync(connection, securityTable, cancellationToken))
            return;

        var wFormIdValue = GetWFormReferenceIdValue(formId);

        const string existsSql = "SELECT COUNT(1) FROM dbo.wFormSecurity WHERE wFormId = @FormId AND isDeleted = 0";
        await using (var existsCmd = new SqlCommand(existsSql, connection))
        {
            existsCmd.Parameters.AddWithValue("@FormId", wFormIdValue);
            if (Convert.ToInt32(await existsCmd.ExecuteScalarAsync(cancellationToken)) > 0)
                return;
        }
        var securityIdIsIdentity = await IsIdentityColumnAsync(connection, securityTable, "id", cancellationToken);

        foreach (var userId in userIds)
        {
            var cols = "wFormId, userId, createdAt, createdBy, isDeleted";
            var vals = "@FormId, @UserId, @CreatedAt, @CreatedBy, 0";
            int? secId = null;
            if (!securityIdIsIdentity)
            {
                secId = await GetNextNumericIdAsync(connection, securityTable, cancellationToken);
                cols = "id, " + cols;
                vals = "@Id, " + vals;
            }

            var insertSql = $"INSERT INTO dbo.[{securityTable}]({cols}) VALUES({vals});";
            await using var cmd = new SqlCommand(insertSql, connection);
            if (secId.HasValue)
                cmd.Parameters.AddWithValue("@Id", secId.Value);
            cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CreatedAt", now);
            cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SyncFormControlsAsync(
        SqlConnection connection,
        string formId,
        List<FormPanelDto> panels,
        List<FormPanelDto> secondaryPanels,
        List<FormFieldDto> topLevelFields,
        string createdBy,
        string now,
        CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "wFormControl", cancellationToken))
            return;

        var wFormIdValue = GetWFormReferenceIdValue(formId);

        await using (var delCmd = new SqlCommand("DELETE FROM dbo.wFormControl WHERE wFormId = @FormId", connection))
        {
            delCmd.Parameters.AddWithValue("@FormId", wFormIdValue);
            await delCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var field in topLevelFields)
        {
            var parentId = await InsertControlAsync(connection, wFormIdValue, field, 0, createdBy, now, cancellationToken);

            if (string.Equals(field.Type, "TABLE", StringComparison.OrdinalIgnoreCase)
                && field.Settings?.Specific?.TableColumns != null)
            {
                foreach (var col in field.Settings.Specific.TableColumns)
                {
                    if (col.Type != null && !SkippedFieldTypes.Contains(col.Type) && !string.IsNullOrWhiteSpace(col.Id))
                        await InsertControlAsync(connection, wFormIdValue, col, parentId, createdBy, now, cancellationToken);
                }
            }
            else if (string.Equals(field.Type, "POPUP", StringComparison.OrdinalIgnoreCase)
                     && field.Settings?.Specific != null
                     && secondaryPanels.Count > 0)
            {
                var panelIndex = field.Settings.Specific.MappedPopupPanel;
                if (panelIndex >= 0 && panelIndex < secondaryPanels.Count && secondaryPanels[panelIndex].Fields != null)
                {
                    foreach (var popupField in secondaryPanels[panelIndex].Fields!)
                    {
                        if (popupField.Type != null && !SkippedFieldTypes.Contains(popupField.Type) && !string.IsNullOrWhiteSpace(popupField.Id))
                            await InsertControlAsync(connection, wFormIdValue, popupField, parentId, createdBy, now, cancellationToken);
                    }
                }
            }
        }
    }

    private static async Task<int> InsertControlAsync(
        SqlConnection connection,
        object wFormIdValue,
        FormFieldDto field,
        int parentId,
        string createdBy,
        string now,
        CancellationToken cancellationToken)
    {
        var validationJson = BuildValidationJson(field);
        var fieldRule = field.Settings?.Validation?.FieldRule;
        var isMandatory = string.Equals(fieldRule, "required", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fieldRule, "REQUIRED", StringComparison.Ordinal);

        const string controlTable = "wFormControl";
        var cols = "wFormId, jsonId, name, type, isMandatory, parentId, createdAt, createdBy, isDeleted, validationJson";
        var vals = "@FormId, @JsonId, @Name, @Type, @Mandatory, @ParentId, @CreatedAt, @CreatedBy, 0, @ValidationJson";
        var controlIdSqlType = await GetColumnTypeAsync(connection, controlTable, "id", cancellationToken);
        int? explicitId = null;
        if (!await IsIdentityColumnAsync(connection, controlTable, "id", cancellationToken))
        {
            explicitId = await GetNextNumericIdAsync(connection, controlTable, cancellationToken);
            cols = "id, " + cols;
            vals = "@Id, " + vals;
        }

        var sql = $@"
INSERT INTO dbo.[{controlTable}]({cols})
OUTPUT INSERTED.id
VALUES({vals});";

        await using var cmd = new SqlCommand(sql, connection);
        if (explicitId.HasValue)
        {
            cmd.Parameters.AddWithValue("@Id", IsNumericSqlType(controlIdSqlType)
                ? explicitId.Value
                : explicitId.Value.ToString());
        }
        cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
        cmd.Parameters.AddWithValue("@JsonId", field.Id!);
        cmd.Parameters.AddWithValue("@Name", (object?)field.Label ?? field.Id!);
        cmd.Parameters.AddWithValue("@Type", (object?)field.Type ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Mandatory", isMandatory);
        cmd.Parameters.AddWithValue("@ParentId", parentId);
        cmd.Parameters.AddWithValue("@CreatedAt", now);
        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
        cmd.Parameters.AddWithValue("@ValidationJson", (object?)validationJson ?? DBNull.Value);

        var idObj = await cmd.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("wFormControl insert did not return an id.");
        return ParseNumericId(idObj, "wFormControl");
    }

    private static string? BuildValidationJson(FormFieldDto field)
    {
        var type = field.Type ?? "";
        var validation = field.Settings?.Validation;
        var specific = field.Settings?.Specific;

        if (type is "SINGLE_CHOICE" or "SINGLE_SELECT")
        {
            var options = (specific?.CustomOptions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            return JsonSerializer.Serialize(new { validation = new { }, specific = new { customOptions = options } });
        }

        if (type is "SHORT_TEXT" or "NUMBER" && validation != null)
        {
            return JsonSerializer.Serialize(new
            {
                specific = new { },
                validation = new
                {
                    validation.ContentRule,
                    validation.Maximum,
                    validation.Minimum
                }
            });
        }

        return null;
    }

    private static async Task EnsureFormEntryTableAsync(
        SqlConnection connection,
        string formId,
        List<FormFieldDto> fields,
        CancellationToken cancellationToken)
    {
        var tableSuffix = FormIdNaming.GetEzfbTableSuffix(formId);
        var tableName = $"ezfb_{tableSuffix}_items";
        var checkSql = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName";
        await using (var checkCmd = new SqlCommand(checkSql, connection))
        {
            checkCmd.Parameters.AddWithValue("@TableName", tableName);
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0)
                return;
        }

        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE [dbo].[{tableName}] (");
        sb.Append("[itemId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,");

        foreach (var field in fields)
        {
            var col = EscapeSqlIdentifier(field.Id!);
            sb.Append('[').Append(col).Append("] NVARCHAR(MAX) NULL,");
        }

        sb.Append("[createdAt] NVARCHAR(50) NULL,[modifiedAt] NVARCHAR(50) NULL,");
        sb.Append("[createdBy] NVARCHAR(50) NOT NULL DEFAULT('0'),[modifiedBy] NVARCHAR(50) NOT NULL DEFAULT('0'),");
        sb.Append("[isDeleted] BIT NOT NULL DEFAULT(0),[todayTask] BIT NOT NULL DEFAULT(1),[isMarked] BIT NOT NULL DEFAULT(0),");
        sb.Append("[ValidFrom] DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN DEFAULT SYSUTCDATETIME(),");
        sb.Append("[ValidTo] DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN DEFAULT CONVERT(DATETIME2, '9999-12-31 23:59:59.9999999'),");
        sb.Append($"PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.ezfb_{tableSuffix}_history));");

        await using var createCmd = new SqlCommand(sb.ToString(), connection) { CommandTimeout = 120 };
        await createCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string EscapeSqlIdentifier(string name) =>
        EzfbColumnNaming.ToSqlBracketIdentifier(name);

    private static async Task<object> ResolveTenantKeyAsync(SqlConnection conn, Guid tenantGuid, CancellationToken cancellationToken)
    {
        var tenantType = await GetColumnTypeAsync(conn, "wForm", "tenantId", cancellationToken);
        if (tenantType is "int" or "bigint" or "smallint" or "tinyint")
            return await EnsureTenantIntIdAsync(conn, tenantGuid, cancellationToken);
        return tenantGuid;
    }

    private static async Task<int> EnsureTenantIntIdAsync(SqlConnection conn, Guid tenantGuid, CancellationToken cancellationToken)
    {
        const string ensureSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantIdMap' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.TenantIdMap(
        TenantGuid UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TenantIntId INT IDENTITY(1,1) NOT NULL UNIQUE,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END";
        await using (var ensureCmd = new SqlCommand(ensureSql, conn))
            await ensureCmd.ExecuteNonQueryAsync(cancellationToken);

        const string getSql = "SELECT TenantIntId FROM dbo.TenantIdMap WHERE TenantGuid = @TenantGuid";
        await using (var getCmd = new SqlCommand(getSql, conn))
        {
            getCmd.Parameters.AddWithValue("@TenantGuid", tenantGuid);
            var existing = await getCmd.ExecuteScalarAsync(cancellationToken);
            if (existing != null && existing != DBNull.Value)
                return Convert.ToInt32(existing);
        }

        const string insertSql = @"
INSERT INTO dbo.TenantIdMap(TenantGuid) VALUES(@TenantGuid);
SELECT TenantIntId FROM dbo.TenantIdMap WHERE TenantGuid = @TenantGuid;";
        await using var insertCmd = new SqlCommand(insertSql, conn);
        insertCmd.Parameters.AddWithValue("@TenantGuid", tenantGuid);
        return Convert.ToInt32(await insertCmd.ExecuteScalarAsync(cancellationToken));
    }

    private static Task<bool> HasTableAsync(SqlConnection conn, string tableName, CancellationToken cancellationToken) =>
        TableExistsAsync(conn, tableName, cancellationToken);

    private static async Task<bool> HasColumnAsync(SqlConnection conn, string tableName, string columnName, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='dbo' AND LOWER(TABLE_NAME)=LOWER(@TableName) AND LOWER(COLUMN_NAME)=LOWER(@ColumnName)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        cmd.Parameters.AddWithValue("@ColumnName", columnName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<string?> GetColumnTypeAsync(SqlConnection conn, string tableName, string columnName, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT TOP 1 DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='dbo' AND LOWER(TABLE_NAME)=LOWER(@TableName) AND LOWER(COLUMN_NAME)=LOWER(@ColumnName)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        cmd.Parameters.AddWithValue("@ColumnName", columnName);
        return (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString()?.ToLowerInvariant();
    }

    private static bool IsNumericSqlType(string? dataType) =>
        dataType is "int" or "bigint" or "smallint" or "tinyint";

    private static int ParseNumericId(object idObj, string context)
    {
        if (idObj is int i)
            return i;
        if (idObj is long l)
            return Convert.ToInt32(l);
        var text = Convert.ToString(idObj)?.Trim();
        if (int.TryParse(text, out var parsed))
            return parsed;
        throw new InvalidOperationException($"{context} id '{text}' is not numeric.");
    }

    private static async Task<int?> GetColumnCharMaxLengthAsync(
        SqlConnection conn,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='dbo' AND LOWER(TABLE_NAME)=LOWER(@TableName) AND LOWER(COLUMN_NAME)=LOWER(@ColumnName)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        cmd.Parameters.AddWithValue("@ColumnName", columnName);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        if (value == null || value == DBNull.Value)
            return null;

        var length = Convert.ToInt32(value);
        // varchar(max)
        if (length < 0)
            return 64;

        return length;
    }

    /// <summary>Always allocates a new dashed GUID for dbo.wForm.id (designer uid stays in wForm.uid only).</summary>
    private static async Task<string> ResolveNewFormIdAsync(
        SqlConnection connection,
        FormJsonDto formJson,
        int? idMaxLength,
        CancellationToken cancellationToken)
    {
        _ = formJson;
        var maxLen = idMaxLength is > 0 and <= 128 ? idMaxLength.Value : 36;
        if (maxLen < 36)
            throw new InvalidOperationException(
                $"dbo.wForm.id allows only {maxLen} characters. Widen the column to NVARCHAR(64) to use dashed GUID form ids.");

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = FormIdNaming.GenerateFormId();
            if (!await FormIdExistsAsync(connection, candidate, cancellationToken))
                return candidate;
        }

        throw new InvalidOperationException("Could not allocate a unique form id.");
    }

    private static async Task<bool> FormIdExistsAsync(
        SqlConnection connection,
        string formId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(1) FROM dbo.wForm WHERE id = @Id";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", formId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static object CoerceIdValue(string formId, string? sqlType) =>
        sqlType switch
        {
            "uniqueidentifier" => Guid.Parse(formId.Length == 32
                ? $"{formId[..8]}-{formId[8..12]}-{formId[12..16]}-{formId[16..20]}-{formId[20..]}"
                : formId),
            "int" or "bigint" or "smallint" or "tinyint" =>
                throw new InvalidOperationException(
                    "wForm.id is numeric in this database. Use a tenant DB where wForm.id is NVARCHAR/UNIQUEIDENTIFIER for GUID form ids."),
            _ => formId
        };

    /// <summary>Same value as dbo.wForm.id (full GUID string).</summary>
    private static object GetWFormReferenceIdValue(string formId) => formId;

    /// <summary>Legacy v5 tenant DBs often use INT id without IDENTITY on wForm / wFormControl.</summary>
    private static async Task<bool> IsIdentityColumnAsync(
        SqlConnection conn,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT CASE WHEN COLUMNPROPERTY(OBJECT_ID(@FullName), @Column, 'IsIdentity') = 1 THEN 1 ELSE 0 END";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@FullName", $"dbo.{tableName}");
        cmd.Parameters.AddWithValue("@Column", columnName);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value != null && value != DBNull.Value && Convert.ToInt32(value) == 1;
    }

    /// <summary>
    /// Next id for legacy tables where id is INT or NVARCHAR with mixed values (e.g. hex '50EC2675').
    /// Only numeric ids participate in MAX; hex/uids are ignored.
    /// </summary>
    private static async Task<int> GetNextNumericIdAsync(
        SqlConnection conn,
        string tableName,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT ISNULL((
    SELECT MAX(TRY_CAST(id AS BIGINT))
    FROM dbo.[{tableName}] WITH (UPDLOCK, HOLDLOCK)
    WHERE TRY_CAST(id AS BIGINT) IS NOT NULL
), 0) + 1";
        await using var cmd = new SqlCommand(sql, conn);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }
}
