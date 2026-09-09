using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace SaaSApp.Reporting.Infrastructure.Services;

internal static class ReportEzfbNaming
{
    public const int SuffixLength = 8;

    public static string NormalizeFormId(string formId)
    {
        var trimmed = formId.Trim();
        return Guid.TryParse(trimmed, out var guid) ? guid.ToString("D") : trimmed;
    }

    public static string GetTableSuffix(string formId)
    {
        var compact = string.Concat(formId.Where(char.IsLetterOrDigit));
        if (compact.Length == 0)
            throw new InvalidOperationException($"Invalid form id for ezfb table suffix: '{formId}'.");

        return compact.Length <= SuffixLength
            ? compact.ToLowerInvariant()
            : compact[..SuffixLength].ToLowerInvariant();
    }

    public static string ItemsTable(string formId) => $"ezfb_{GetTableSuffix(formId)}_items";

    public static async Task<IReadOnlyList<(string FormId, string FormName, string? Type)>> ListWFormsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "wForm", cancellationToken))
            return [];

        const string sql = """
            SELECT CONVERT(NVARCHAR(64), id), name, type
            FROM dbo.wForm
            WHERE isDeleted = 0
            ORDER BY name;
            """;
        var rows = new List<(string, string, string?)>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((
                reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    public static async Task<(string FormId, string FormName, string? Type)?> FindWFormAsync(
        SqlConnection connection,
        string? key,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        if (!await TableExistsAsync(connection, "wForm", cancellationToken))
            return null;

        var token = key.Trim();
        const string sql = """
            SELECT TOP 1 CONVERT(NVARCHAR(64), id), name, type
            FROM dbo.wForm
            WHERE isDeleted = 0
              AND (
                    CONVERT(NVARCHAR(64), id) = @Key
                 OR LOWER(REPLACE(CONVERT(NVARCHAR(64), id), '-', '')) = @Compact
                 OR LOWER(LTRIM(RTRIM(name))) = LOWER(@Key)
              )
            ORDER BY CASE
                WHEN CONVERT(NVARCHAR(64), id) = @Key THEN 0
                WHEN LOWER(LTRIM(RTRIM(name))) = LOWER(@Key) THEN 1
                ELSE 2 END;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Key", token);
        cmd.Parameters.AddWithValue("@Compact", string.Concat(token.Where(char.IsLetterOrDigit)).ToLowerInvariant());
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return (
            reader.GetString(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }


    /// <summary>workflow.processForm_{suffix} uses the first 8 hex chars of the workflow GUID (N format).</summary>
    public static string WorkflowTableSuffix(Guid workflowId) => workflowId.ToString("N")[..SuffixLength];

    public static bool LooksLikeWorkflowId(string? value, Guid workflowId)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var trimmed = value.Trim();
        if (Guid.TryParse(trimmed, out var guid) && guid == workflowId)
            return true;
        var compact = string.Concat(trimmed.Where(char.IsLetterOrDigit));
        var wf = workflowId.ToString("N");
        return compact.Equals(wf, StringComparison.OrdinalIgnoreCase)
               || compact.Equals(wf[..SuffixLength], StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = @Schema AND t.name = @TableName;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        return await cmd.ExecuteScalarAsync(cancellationToken) != null;
    }

    public static async Task<string?> TryReadProcessFormWFormIdAsync(
        SqlConnection connection,
        Guid workflowId,
        CancellationToken cancellationToken)
    {
        var suffix = WorkflowTableSuffix(workflowId);
        var tableName = $"processForm_{suffix}";
        if (!await TableExistsAsync(connection, "workflow", tableName, cancellationToken))
            return null;

        var sql = $"""
            SELECT TOP 1 CONVERT(NVARCHAR(64), WFormId)
            FROM workflow.[{tableName}]
            WHERE (IsDeleted = 0 OR IsDeleted IS NULL)
              AND WFormId IS NOT NULL
            ORDER BY Id DESC;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
            return null;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static string EscapeColumn(string column) => column.Replace("]", "]]", StringComparison.Ordinal);

    public static bool TryToColumnName(string jsonId, out string column)
    {
        var safe = new string(jsonId.Where(static c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        column = safe;
        return !string.IsNullOrEmpty(safe);
    }

    public static bool TryResolveColumn(string jsonId, IReadOnlySet<string> ezfbColumns, out string? column)
    {
        column = null;
        if (string.IsNullOrWhiteSpace(jsonId) || ezfbColumns.Count == 0)
            return false;

        var trimmed = jsonId.Trim();
        if (ezfbColumns.Contains(trimmed))
        {
            column = trimmed;
            return true;
        }

        if (TryToColumnName(trimmed, out var fromJsonId) && ezfbColumns.Contains(fromJsonId))
        {
            column = fromJsonId;
            return true;
        }

        var compact = CompactToken(trimmed);
        if (compact.Length > 0 && ezfbColumns.Contains(compact))
        {
            column = compact;
            return true;
        }

        var underscored = trimmed.Replace('-', '_');
        if (ezfbColumns.Contains(underscored))
        {
            column = underscored;
            return true;
        }

        if (compact.Length > 0 && char.IsDigit(compact[0]))
        {
            foreach (var legacy in new[] { "F_" + compact, "F_" + trimmed, "F_" + underscored })
            {
                if (ezfbColumns.Contains(legacy))
                {
                    column = legacy;
                    return true;
                }
            }
        }

        foreach (var existing in ezfbColumns)
        {
            if (CompactToken(existing).Equals(compact, StringComparison.OrdinalIgnoreCase))
            {
                column = existing;
                return true;
            }
        }

        return false;
    }

    public static string CompactToken(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit));

    public static string DisplayCell(object? value)
    {
        var raw = CellString(value);
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var trimmed = raw.Trim();
        if (trimmed.Length < 2
            || (trimmed[0] != '{' && trimmed[0] != '['))
            return raw;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var display = FlattenJsonValue(doc.RootElement);
            return string.IsNullOrWhiteSpace(display) ? raw : display;
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private static string FlattenJsonValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return string.Empty;
            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetRawText();
            case JsonValueKind.Array:
                return string.Join(
                    ", ",
                    element.EnumerateArray()
                        .Select(FlattenJsonValue)
                        .Where(v => !string.IsNullOrWhiteSpace(v)));
            case JsonValueKind.Object:
                foreach (var name in new[] { "name", "label", "text", "displayName", "title", "value" })
                {
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var nested = FlattenJsonValue(prop.Value);
                        if (!string.IsNullOrWhiteSpace(nested))
                            return nested;
                    }
                }

                return string.Join(
                    ", ",
                    element.EnumerateObject()
                        .Select(p => FlattenJsonValue(p.Value))
                        .Where(v => !string.IsNullOrWhiteSpace(v)));
            default:
                return element.ToString();
        }
    }

    public static bool IsSystemColumn(string column) =>
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

    public static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken) =>
        await TableExistsAsync(connection, "dbo", tableName, cancellationToken);

    public static async Task<HashSet<string>> LoadTableColumnsAsync(
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

    public static async Task<object> ResolveWFormIdParameterAsync(
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
            if (hex.Length > SuffixLength)
                hex = hex[..SuffixLength];
            if (uint.TryParse(hex.PadLeft(SuffixLength, '0'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u))
                return unchecked((int)u);
        }

        return formId;
    }

    public static string CellString(object? value)
    {
        if (value is null or DBNull)
            return string.Empty;
        if (value is DateTime dt)
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (value is DateTimeOffset dto)
            return dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (value is byte[] bytes)
            return Encoding.UTF8.GetString(bytes);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
