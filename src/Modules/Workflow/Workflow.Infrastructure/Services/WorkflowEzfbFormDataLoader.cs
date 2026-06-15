using System.Globalization;
using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>Reads ezfb row field values as JSON (jsonId keys) for inbox display.</summary>
public sealed class WorkflowEzfbFormDataLoader : SaaSApp.Workflow.Application.Contracts.IWorkflowEzfbFormDataLoader
{
    private readonly ITenantContext _tenantContext;

    public WorkflowEzfbFormDataLoader(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task<string?> LoadFormDataJsonAsync(
        string formId,
        int formEntryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formId) || formEntryId <= 0)
            return null;

        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await LoadFormDataJsonAsync(connection, formId, formEntryId, cancellationToken);
    }

    internal static async Task<string?> LoadFormDataJsonAsync(
        SqlConnection connection,
        string rawFormId,
        int formEntryId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawFormId) || formEntryId <= 0)
            return null;

        string tableSuffix;
        try
        {
            tableSuffix = FormIdNaming.GetEzfbTableSuffix(FormIdNaming.NormalizeFormId(rawFormId));
        }
        catch
        {
            return null;
        }

        var tableName = $"ezfb_{tableSuffix}_items";
        const string objectSql = "SELECT OBJECT_ID(@TableName, 'U');";
        await using (var objectCmd = new SqlCommand(objectSql, connection))
        {
            objectCmd.Parameters.AddWithValue("@TableName", $"dbo.{tableName}");
            var objectId = await objectCmd.ExecuteScalarAsync(cancellationToken);
            if (objectId == null || objectId == DBNull.Value)
                return null;
        }

        var ezfbColumns = await LoadTableColumnsAsync(connection, tableName, cancellationToken);
        if (ezfbColumns.Count == 0)
            return null;

        var normalizedFormId = FormIdNaming.NormalizeFormId(rawFormId);
        var wFormIdValue = await ResolveWFormIdParameterAsync(connection, normalizedFormId, cancellationToken);
        var controls = await LoadFormControlJsonIdsAsync(connection, wFormIdValue, cancellationToken);

        var selectColumns = new List<string>();
        foreach (var jsonId in controls)
        {
            if (TryResolveEzfbColumn(jsonId, ezfbColumns, out var col)
                && !selectColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
            {
                selectColumns.Add(col);
            }
        }

        if (selectColumns.Count == 0)
        {
            selectColumns = ezfbColumns
                .Where(c => !IsSystemColumn(c))
                .ToList();
        }

        if (selectColumns.Count == 0)
            return null;

        var selectList = string.Join(", ", selectColumns.Select(c =>
            $"ISNULL(CONVERT(NVARCHAR(MAX), [{EscapeColumn(c)}]), N'') AS [{EscapeColumn(c)}]"));

        var dataSql = $@"
SELECT {selectList}
FROM dbo.[{tableName}]
WHERE itemId = @ItemId AND (isDeleted = 0 OR isDeleted IS NULL)
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;";

        await using var dataCmd = new SqlCommand(dataSql, connection);
        dataCmd.Parameters.AddWithValue("@ItemId", formEntryId);
        var data = await dataCmd.ExecuteScalarAsync(cancellationToken);
        return data == null || data == DBNull.Value ? null : Convert.ToString(data);
    }

    private static bool IsSystemColumn(string column) =>
        column.Equals("itemId", StringComparison.OrdinalIgnoreCase)
        || column.Equals("createdAt", StringComparison.OrdinalIgnoreCase)
        || column.Equals("modifiedAt", StringComparison.OrdinalIgnoreCase)
        || column.Equals("createdBy", StringComparison.OrdinalIgnoreCase)
        || column.Equals("modifiedBy", StringComparison.OrdinalIgnoreCase)
        || column.Equals("isDeleted", StringComparison.OrdinalIgnoreCase)
        || column.Equals("todayTask", StringComparison.OrdinalIgnoreCase)
        || column.Equals("isMarked", StringComparison.OrdinalIgnoreCase);

    private static string EscapeColumn(string column) => column.Replace("]", "]]");

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

    private static async Task<List<string>> LoadFormControlJsonIdsAsync(
        SqlConnection connection,
        object wFormIdValue,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT jsonId
            FROM dbo.wFormControl
            WHERE wFormId = @FormId
              AND isDeleted = 0
              AND jsonId IS NOT NULL
              AND LTRIM(RTRIM(jsonId)) <> ''
            """;
        var jsonIds = new List<string>();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            jsonIds.Add(reader.GetString(0));
        return jsonIds;
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
