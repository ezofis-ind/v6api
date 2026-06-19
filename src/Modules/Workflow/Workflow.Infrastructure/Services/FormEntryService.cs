using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Forms;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>v5 PostformentryFn / GetformentryidFn parity for dbo.ezfb_{form}_items.</summary>
public sealed class FormEntryService : IFormEntryService
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowEzfbFormDataLoader _formDataLoader;
    private readonly ILogger<FormEntryService> _logger;

    public FormEntryService(
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        IWorkflowEzfbFormDataLoader formDataLoader,
        ILogger<FormEntryService> logger)
    {
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _formDataLoader = formDataLoader;
        _logger = logger;
    }

    public Task<FormEntryResult> UpsertEntryAsync(
        string formId,
        int entryId,
        JsonElement body,
        CancellationToken cancellationToken = default)
    {
        var request = ParseUpsertRequest(body);
        return UpsertEntryAsync(formId, entryId, request, cancellationToken);
    }

    public async Task<FormEntryResult> UpsertEntryAsync(
        string formId,
        int entryId,
        FormEntryUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formId))
            return Failed("form id is required.");

        var normalizedFormId = FormIdNaming.NormalizeFormId(formId);
        var fieldMap = NormalizeFields(request.Fields);
        if (fieldMap.Count == 0)
            return Failed("fields are required.");

        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");
        var userId = _currentUserProvider.GetUserId()
            ?? throw new InvalidOperationException("Authenticated user is required.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var formMeta = await LoadFormMetadataAsync(connection, normalizedFormId, cancellationToken);
        if (formMeta == null)
            return Failed("form entry not found");

        var tableSuffix = FormIdNaming.GetEzfbTableSuffix(normalizedFormId);
        var tableName = $"ezfb_{tableSuffix}_items";
        if (!await EzfbTableExistsAsync(connection, tableName, cancellationToken))
            return Failed("form entry table not found");

        var ezfbColumns = await LoadTableColumnsAsync(connection, tableName, cancellationToken);
        if (ezfbColumns.Count == 0)
            return Failed("form entry table has no columns");

        var wFormIdValue = await ResolveWFormIdParameterAsync(connection, normalizedFormId, cancellationToken);
        var controls = await LoadFormControlsAsync(connection, wFormIdValue, cancellationToken);
        var resolvedFields = ResolveFieldColumns(fieldMap, controls, ezfbColumns);
        if (resolvedFields.Count == 0)
            return Failed("no matching ezfb columns for submitted fields");

        var duplicate = await CheckUniqueColumnsAsync(
            connection,
            tableName,
            formMeta.UniqueColumns,
            resolvedFields,
            entryId > 0 ? entryId : null,
            controls,
            cancellationToken);
        if (duplicate != null)
            return duplicate;

        var userStamp = userId.ToString("D");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        if (entryId == 0)
        {
            var newId = await InsertEntryAsync(
                connection,
                tableName,
                resolvedFields,
                now,
                userStamp,
                cancellationToken);
            if (newId <= 0)
                return Failed("form entry not found");

            return new FormEntryResult(1, newId.ToString(CultureInfo.InvariantCulture), "created new form entry");
        }

        if (!await EntryExistsAsync(connection, tableName, entryId, cancellationToken))
            return Failed("form entry not found");

        var updated = await UpdateEntryAsync(
            connection,
            tableName,
            entryId,
            resolvedFields,
            now,
            userStamp,
            cancellationToken);
        if (!updated)
            return Failed("form entry not found");

        return new FormEntryResult(
            2,
            entryId.ToString(CultureInfo.InvariantCulture),
            "updated form entry");
    }

    public async Task<FormEntryGetResult> GetEntriesAsync(
        string formId,
        string entryIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formId) || string.IsNullOrWhiteSpace(entryIds))
            return new FormEntryGetResult(FormEntryGetStatus.NotFound, null);

        var ids = entryIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return new FormEntryGetResult(FormEntryGetStatus.NotFound, null);

        var normalizedFormId = FormIdNaming.NormalizeFormId(formId);
        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableSuffix = FormIdNaming.GetEzfbTableSuffix(normalizedFormId);
        var tableName = $"ezfb_{tableSuffix}_items";
        if (!await EzfbTableExistsAsync(connection, tableName, cancellationToken))
            return new FormEntryGetResult(FormEntryGetStatus.NotFound, null);

        var ezfbColumns = await LoadTableColumnsAsync(connection, tableName, cancellationToken);
        var entries = new List<Dictionary<string, object?>>();

        foreach (var entryId in ids)
        {
            var row = await LoadEntryRowAsync(connection, tableName, ezfbColumns, entryId, cancellationToken);
            if (row == null)
                continue;

            var formDataJson = await _formDataLoader.LoadFormDataJsonAsync(normalizedFormId, entryId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(formDataJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(formDataJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                            row[prop.Name] = JsonElementToObject(prop.Value);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogDebug(ex, "Could not merge jsonId form data for entry {EntryId}.", entryId);
                }
            }

            entries.Add(row);
        }

        return entries.Count == 0
            ? new FormEntryGetResult(FormEntryGetStatus.NotFound, null)
            : new FormEntryGetResult(FormEntryGetStatus.Found, entries);
    }

    private static FormEntryUpsertRequest ParseUpsertRequest(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
            return new FormEntryUpsertRequest();

        if (!body.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
            return new FormEntryUpsertRequest();

        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in fields.EnumerateObject())
            dict[prop.Name] = prop.Value;

        return new FormEntryUpsertRequest { Fields = dict };
    }

    private static Dictionary<string, string> NormalizeFields(Dictionary<string, JsonElement>? fields)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (fields == null)
            return map;

        foreach (var (key, value) in fields)
        {
            var text = JsonElementToStorageString(value);
            if (!string.IsNullOrWhiteSpace(text))
                map[key.Trim()] = text;
        }

        return map;
    }

    private static string? JsonElementToStorageString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Array or JsonValueKind.Object => value.GetRawText(),
        _ => value.GetRawText()
    };

    private static object? JsonElementToObject(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array or JsonValueKind.Object => JsonSerializer.Deserialize<object>(value.GetRawText()),
        _ => value.GetRawText()
    };

    private static FormEntryResult Failed(string message) =>
        new(0, null, message);

    private static async Task<FormMetadata?> LoadFormMetadataAsync(
        SqlConnection connection,
        string formId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 1 uniqueColumns
            FROM dbo.wForm
            WHERE id = @FormId AND isDeleted = 0
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FormId", formId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var uniqueColumns = reader.IsDBNull(0) ? null : reader.GetString(0);
        return new FormMetadata(uniqueColumns);
    }

    private sealed record FormMetadata(string? UniqueColumns);

    private sealed record FormControlRow(string JsonId, string? Name, string? Type);

    private static List<KeyValuePair<string, string>> ResolveFieldColumns(
        IReadOnlyDictionary<string, string> fields,
        IReadOnlyList<FormControlRow> controls,
        IReadOnlySet<string> ezfbColumns)
    {
        var resolved = new List<KeyValuePair<string, string>>();
        foreach (var (key, value) in fields)
        {
            if (TryResolveControlForField(key, controls, out var control) && control is not null)
            {
                if (TryResolveEzfbColumn(control.JsonId, ezfbColumns, out var col))
                    resolved.Add(new KeyValuePair<string, string>(col, value));
                continue;
            }

            if (TryResolveEzfbColumn(key, ezfbColumns, out var colFromKey))
                resolved.Add(new KeyValuePair<string, string>(colFromKey, value));
        }

        return resolved;
    }

    private static async Task<FormEntryResult?> CheckUniqueColumnsAsync(
        SqlConnection connection,
        string tableName,
        string? uniqueColumns,
        IReadOnlyList<KeyValuePair<string, string>> resolvedFields,
        int? excludeEntryId,
        IReadOnlyList<FormControlRow> controls,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uniqueColumns))
            return null;

        var fieldByColumn = resolvedFields.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<FormEntryDuplicateField>();

        foreach (var rawColumn in uniqueColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryResolveEzfbColumn(rawColumn, fieldByColumn.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase), out var ezfbCol))
            {
                if (!fieldByColumn.ContainsKey(rawColumn))
                    continue;
                ezfbCol = rawColumn;
            }

            if (!fieldByColumn.TryGetValue(ezfbCol, out var fieldValue))
                continue;

            var existingId = await FindDuplicateEntryIdAsync(
                connection,
                tableName,
                ezfbCol,
                fieldValue,
                excludeEntryId,
                cancellationToken);
            if (existingId == null)
                continue;

            var controlName = controls
                .FirstOrDefault(c => string.Equals(c.JsonId, rawColumn, StringComparison.OrdinalIgnoreCase))
                ?.Name;
            duplicates.Add(new FormEntryDuplicateField(rawColumn, controlName, fieldValue));
        }

        if (duplicates.Count == 0)
            return null;

        return new FormEntryResult(
            3,
            JsonSerializer.Serialize(duplicates),
            "already existing form entry");
    }

    private static async Task<int?> FindDuplicateEntryIdAsync(
        SqlConnection connection,
        string tableName,
        string column,
        string fieldValue,
        int? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var escapedCol = EscapeColumn(column);
        string sql;
        if (string.IsNullOrWhiteSpace(fieldValue))
        {
            sql = $"""
                SELECT TOP 1 itemId
                FROM dbo.[{tableName}]
                WHERE [{escapedCol}] = N'' AND [{escapedCol}] <> N''
                  AND (isDeleted = 0 OR isDeleted IS NULL)
                """;
        }
        else
        {
            sql = $"""
                SELECT TOP 1 itemId
                FROM dbo.[{tableName}]
                WHERE (
                    [{escapedCol}] = @Value
                    OR (ISJSON([{escapedCol}]) = 1 AND JSON_VALUE([{escapedCol}], '$.email') = @Value)
                )
                  AND (isDeleted = 0 OR isDeleted IS NULL)
                """;
        }

        if (excludeEntryId is > 0)
            sql += " AND itemId <> @ExcludeId";

        await using var cmd = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(fieldValue))
            cmd.Parameters.AddWithValue("@Value", fieldValue);
        if (excludeEntryId is > 0)
            cmd.Parameters.AddWithValue("@ExcludeId", excludeEntryId.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result == null || result == DBNull.Value)
            return null;

        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<int> InsertEntryAsync(
        SqlConnection connection,
        string tableName,
        IReadOnlyList<KeyValuePair<string, string>> resolvedFields,
        string createdAt,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var columns = resolvedFields.Select(f => $"[{EscapeColumn(f.Key)}]").ToList();
        columns.Add("[createdAt]");
        columns.Add("[createdBy]");
        columns.Add("[isDeleted]");

        var values = resolvedFields.Select((_, i) => $"@V{i}").ToList();
        values.Add("@CreatedAt");
        values.Add("@CreatedBy");
        values.Add("0");

        var sql = $"""
            INSERT INTO dbo.[{tableName}] ({string.Join(", ", columns)})
            OUTPUT INSERTED.itemId
            VALUES ({string.Join(", ", values)})
            """;

        await using var cmd = new SqlCommand(sql, connection);
        for (var i = 0; i < resolvedFields.Count; i++)
            cmd.Parameters.AddWithValue($"@V{i}", resolvedFields[i].Value);
        cmd.Parameters.AddWithValue("@CreatedAt", createdAt);
        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> UpdateEntryAsync(
        SqlConnection connection,
        string tableName,
        int entryId,
        IReadOnlyList<KeyValuePair<string, string>> resolvedFields,
        string modifiedAt,
        string modifiedBy,
        CancellationToken cancellationToken)
    {
        var sets = resolvedFields
            .Select((f, i) => $"[{EscapeColumn(f.Key)}] = @V{i}")
            .ToList();
        sets.Add("[modifiedAt] = @ModifiedAt");
        sets.Add("[modifiedBy] = @ModifiedBy");

        var sql = $"""
            UPDATE dbo.[{tableName}]
            SET {string.Join(", ", sets)}
            WHERE itemId = @ItemId AND (isDeleted = 0 OR isDeleted IS NULL)
            """;

        await using var cmd = new SqlCommand(sql, connection);
        for (var i = 0; i < resolvedFields.Count; i++)
            cmd.Parameters.AddWithValue($"@V{i}", resolvedFields[i].Value);
        cmd.Parameters.AddWithValue("@ModifiedAt", modifiedAt);
        cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
        cmd.Parameters.AddWithValue("@ItemId", entryId);

        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task<bool> EntryExistsAsync(
        SqlConnection connection,
        string tableName,
        int entryId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT COUNT(1)
            FROM dbo.[{tableName}]
            WHERE itemId = @ItemId AND (isDeleted = 0 OR isDeleted IS NULL)
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ItemId", entryId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<Dictionary<string, object?>?> LoadEntryRowAsync(
        SqlConnection connection,
        string tableName,
        IReadOnlySet<string> ezfbColumns,
        int entryId,
        CancellationToken cancellationToken)
    {
        var selectColumns = ezfbColumns
            .Where(c => !IsSystemColumn(c) || c.Equals("itemId", StringComparison.OrdinalIgnoreCase))
            .Select(c => $"[{EscapeColumn(c)}]")
            .ToList();
        if (selectColumns.Count == 0)
            return null;

        var sql = $"""
            SELECT {string.Join(", ", selectColumns)}
            FROM dbo.[{tableName}]
            WHERE itemId = @ItemId AND (isDeleted = 0 OR isDeleted IS NULL)
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ItemId", entryId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static bool IsSystemColumn(string column) =>
        column.Equals("itemId", StringComparison.OrdinalIgnoreCase)
        || column.Equals("createdAt", StringComparison.OrdinalIgnoreCase)
        || column.Equals("modifiedAt", StringComparison.OrdinalIgnoreCase)
        || column.Equals("createdBy", StringComparison.OrdinalIgnoreCase)
        || column.Equals("modifiedBy", StringComparison.OrdinalIgnoreCase)
        || column.Equals("isDeleted", StringComparison.OrdinalIgnoreCase)
        || column.Equals("todayTask", StringComparison.OrdinalIgnoreCase)
        || column.Equals("isMarked", StringComparison.OrdinalIgnoreCase)
        || column.Equals("ValidFrom", StringComparison.OrdinalIgnoreCase)
        || column.Equals("ValidTo", StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> EzfbTableExistsAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = @TableName
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<HashSet<string>> LoadTableColumnsAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = @TableName
            """;
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(reader.GetString(0));
        return columns;
    }

    private static async Task<List<FormControlRow>> LoadFormControlsAsync(
        SqlConnection connection,
        object wFormIdValue,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT jsonId, name, type
            FROM dbo.wFormControl
            WHERE wFormId = @FormId
              AND isDeleted = 0
              AND jsonId IS NOT NULL
              AND LTRIM(RTRIM(jsonId)) <> ''
            """;
        var rows = new List<FormControlRow>();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new FormControlRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    private static async Task<object> ResolveWFormIdParameterAsync(
        SqlConnection connection,
        string formId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'wFormControl' AND COLUMN_NAME = N'wFormId'
            """;
        await using var cmd = new SqlCommand(sql, connection);
        var type = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString()?.ToLowerInvariant();
        if (type is "int" or "bigint" or "smallint" or "tinyint")
        {
            if (int.TryParse(formId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                return n;
            var hex = new string(formId.Where(Uri.IsHexDigit).ToArray());
            if (hex.Length > 8)
                hex = hex[..8];
            if (uint.TryParse(hex.PadLeft(8, '0'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u))
                return unchecked((int)u);
        }

        return formId;
    }

    private static bool TryResolveControlForField(
        string fieldKey,
        IReadOnlyList<FormControlRow> controls,
        out FormControlRow? control)
    {
        control = null;
        if (string.IsNullOrWhiteSpace(fieldKey))
            return false;

        var key = fieldKey.Trim();
        foreach (var row in controls)
        {
            if (!string.IsNullOrWhiteSpace(row.Name)
                && string.Equals(row.Name.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                control = row;
                return true;
            }
        }

        foreach (var row in controls)
        {
            if (string.Equals(row.JsonId, key, StringComparison.OrdinalIgnoreCase))
            {
                control = row;
                return true;
            }

            if (TryToEzfbColumnName(row.JsonId, out var ezfbCol)
                && string.Equals(ezfbCol, key, StringComparison.OrdinalIgnoreCase))
            {
                control = row;
                return true;
            }
        }

        return false;
    }

    private static string EscapeColumn(string column) => column.Replace("]", "]]");

    private static string ToEzfbColumnName(string jsonId)
    {
        var safe = new string(jsonId.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrEmpty(safe))
            throw new ArgumentException($"Invalid jsonId for ezfb column: {jsonId}");
        return safe;
    }

    private static bool TryToEzfbColumnName(string jsonId, out string column)
    {
        try
        {
            column = ToEzfbColumnName(jsonId);
            return true;
        }
        catch (ArgumentException)
        {
            column = string.Empty;
            return false;
        }
    }

    private static bool TryResolveEzfbColumn(string jsonId, IReadOnlySet<string> ezfbColumns, out string column)
    {
        column = string.Empty;
        if (string.IsNullOrWhiteSpace(jsonId))
            return false;

        var trimmed = jsonId.Trim();
        if (ezfbColumns.Contains(trimmed))
        {
            column = trimmed;
            return true;
        }

        if (TryToEzfbColumnName(trimmed, out var fromJsonId) && ezfbColumns.Contains(fromJsonId))
        {
            column = fromJsonId;
            return true;
        }

        if (TryToEzfbColumnName(trimmed, out var baseName)
            && baseName.Length > 0
            && char.IsDigit(baseName[0]))
        {
            var legacy = "F_" + baseName;
            if (ezfbColumns.Contains(legacy))
            {
                column = legacy;
                return true;
            }
        }

        return false;
    }
}
