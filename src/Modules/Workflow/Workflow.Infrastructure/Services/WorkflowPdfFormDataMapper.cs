using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Forms;
using SaaSApp.Workflow.Application.Workflows;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>
/// Maps formData jsonId keys → control names and nests TABLE rows under the table label.
/// </summary>
public sealed class WorkflowPdfFormDataMapper
{
    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly IFormJsonStorageService _formJsonStorage;
    private readonly ILogger<WorkflowPdfFormDataMapper> _logger;

    public WorkflowPdfFormDataMapper(
        ITenantConnectionProvider connectionProvider,
        IFormJsonStorageService formJsonStorage,
        ILogger<WorkflowPdfFormDataMapper> logger)
    {
        _connectionProvider = connectionProvider;
        _formJsonStorage = formJsonStorage;
        _logger = logger;
    }

    public async Task<Dictionary<string, object?>> MapAsync(
        string? formId,
        string? formDataJson,
        IReadOnlyCollection<string>? templateDataKeys = null,
        CancellationToken cancellationToken = default)
    {
        _ = templateDataKeys;
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(formDataJson))
            return result;

        var schema = await LoadSchemaAsync(formId, cancellationToken);
        using var doc = JsonDocument.Parse(formDataJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return result;

        var tableRows = new Dictionary<string, SortedDictionary<int, Dictionary<string, object?>>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(prop.Name))
                continue;

            if (TryAddTableArray(prop.Name, prop.Value, schema, tableRows))
                continue;

            if (prop.Value.ValueKind == JsonValueKind.Array)
                continue;

            if (schema.IsFileField(prop.Name))
                continue;

            if (IsFileAttachmentJson(prop.Value))
                continue;

            if (TryAddFlattenedTableCell(prop.Name, prop.Value, schema, tableRows))
                continue;

            if (schema.IsTableColumnKey(prop.Name))
                continue;

            var scalar = CoerceValue(prop.Value);
            if (scalar == null)
                continue;

            var name = schema.ResolveName(prop.Name);
            if (FormFieldNameResolver.LooksLikeJsonId(name))
                continue;

            result[name] = scalar;
        }

        foreach (var (tableName, rows) in tableRows)
        {
            if (rows.Count == 0)
                continue;

            result[tableName] = rows
                .OrderBy(static pair => pair.Key)
                .Select(static pair => pair.Value)
                .ToList();
        }

        _logger.LogInformation(
            "WorkflowPdfFormDataMapper: formId {FormId}, mapped {ScalarCount} scalar(s) and {TableCount} table(s)",
            formId,
            result.Count(kv => kv.Value is not IList<Dictionary<string, object?>>),
            result.Count(kv => kv.Value is IList<Dictionary<string, object?>>));

        return result;
    }

    public async Task<Dictionary<string, object?>> MapMergedAsync(
        string? formId,
        string? submittedFormDataJson,
        string? ezfbFormDataJson,
        IReadOnlyCollection<string>? templateDataKeys = null,
        CancellationToken cancellationToken = default)
    {
        _ = templateDataKeys;
        var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(ezfbFormDataJson))
            MergeStructured(merged, await MapAsync(formId, ezfbFormDataJson, null, cancellationToken));

        if (!string.IsNullOrWhiteSpace(submittedFormDataJson))
            MergeStructured(merged, await MapAsync(formId, submittedFormDataJson, null, cancellationToken));

        return merged;
    }

    private static void MergeStructured(
        Dictionary<string, object?> target,
        IReadOnlyDictionary<string, object?> source)
    {
        foreach (var (key, value) in source)
            target[key] = value;
    }

    private static bool TryAddTableArray(
        string propName,
        JsonElement value,
        PdfFormSchema schema,
        Dictionary<string, SortedDictionary<int, Dictionary<string, object?>>> tableRows)
    {
        if (value.ValueKind != JsonValueKind.Array)
            return false;

        var tableName = schema.ResolveTableName(propName);
        if (string.IsNullOrWhiteSpace(tableName))
            return false;

        var rowIndex = 1;
        foreach (var row in value.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                rowIndex++;
                continue;
            }

            foreach (var cell in row.EnumerateObject())
            {
                if (schema.IsFileField(cell.Name) || IsFileAttachmentJson(cell.Value))
                    continue;

                var columnName = schema.ResolveName(cell.Name);
                if (FormFieldNameResolver.LooksLikeJsonId(columnName))
                    continue;

                var cellValue = CoerceValue(cell.Value);
                if (cellValue == null)
                    continue;

                AddTableCell(tableRows, tableName, rowIndex, columnName, cellValue);
            }

            rowIndex++;
        }

        return true;
    }

    private static bool TryAddFlattenedTableCell(
        string propName,
        JsonElement value,
        PdfFormSchema schema,
        Dictionary<string, SortedDictionary<int, Dictionary<string, object?>>> tableRows)
    {
        if (!TrySplitRowSuffix(propName, out var baseKey, out var rowIndex))
            return false;

        var columnName = schema.ResolveName(baseKey);
        if (!schema.TryGetTableForColumn(baseKey, columnName, out var tableName))
            return false;

        var cellValue = CoerceValue(value);
        if (cellValue == null)
            return false;

        AddTableCell(tableRows, tableName, rowIndex, columnName, cellValue);
        return true;
    }

    private static void AddTableCell(
        Dictionary<string, SortedDictionary<int, Dictionary<string, object?>>> tableRows,
        string tableName,
        int rowIndex,
        string columnName,
        object cellValue)
    {
        if (!tableRows.TryGetValue(tableName, out var rows))
        {
            rows = new SortedDictionary<int, Dictionary<string, object?>>();
            tableRows[tableName] = rows;
        }

        if (!rows.TryGetValue(rowIndex, out var row))
        {
            row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            rows[rowIndex] = row;
        }

        row[columnName] = cellValue;
    }

    private async Task<PdfFormSchema> LoadSchemaAsync(string? formId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formId))
            return PdfFormSchema.Empty;

        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        var normalizedFormId = FormIdNaming.NormalizeFormId(formId);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var wFormIdCandidates = await FormWFormIdResolver.BuildCandidatesAsync(
            connection,
            normalizedFormId,
            cancellationToken);
        var controls = await LoadFormControlsWithFallbackAsync(connection, wFormIdCandidates, cancellationToken);

        FormJsonDto? formJson = null;
        try
        {
            var formJsonText = await _formJsonStorage.GetFormJsonAsync(normalizedFormId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(formJsonText))
            {
                formJson = JsonSerializer.Deserialize<FormJsonDto>(
                    formJsonText,
                    WorkflowJsonSerializerOptions.Deserialize);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "WorkflowPdfFormDataMapper: form JSON schema fallback failed for formId {FormId}", formId);
        }

        if (controls.Count == 0)
        {
            _logger.LogWarning(
                "WorkflowPdfFormDataMapper: no wFormControl rows for formId {FormId}",
                normalizedFormId);
        }

        return PdfFormSchema.From(controls, formJson);
    }

    private static bool TrySplitRowSuffix(string key, out string baseKey, out int rowIndex)
    {
        baseKey = key;
        rowIndex = 0;

        var lastSpace = key.LastIndexOf(' ');
        if (lastSpace <= 0 || lastSpace >= key.Length - 1)
            return false;

        var suffix = key[(lastSpace + 1)..];
        if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowIndex) || rowIndex <= 0)
            return false;

        baseKey = key[..lastSpace].Trim();
        return !string.IsNullOrWhiteSpace(baseKey);
    }

    private static bool IsFileAttachmentJson(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            return false;

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{'))
            return false;

        return text.Contains("\"fileId\"", StringComparison.OrdinalIgnoreCase)
            || text.Contains("\"itemId\"", StringComparison.OrdinalIgnoreCase);
    }

    private static object? CoerceValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integer))
                    return integer;
                return value.GetDecimal();
            case JsonValueKind.String:
                var text = value.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                return CoerceString(text);
            default:
                var scalar = value.GetRawText();
                return string.IsNullOrWhiteSpace(scalar) ? null : scalar;
        }
    }

    private static object CoerceString(string text)
    {
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
        {
            if (number % 1 == 0)
                return (long)number;
            return number;
        }

        return text;
    }

    private static async Task<List<FormControlRow>> LoadFormControlsWithFallbackAsync(
        SqlConnection connection,
        IReadOnlyList<object> wFormIdCandidates,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in wFormIdCandidates)
        {
            var controls = await LoadFormControlsAsync(connection, candidate, cancellationToken);
            if (controls.Count > 0)
                return controls;
        }

        return [];
    }

    private static async Task<List<FormControlRow>> LoadFormControlsAsync(
        SqlConnection connection,
        object wFormIdValue,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, jsonId, name, type, ISNULL(parentId, 0)
            FROM dbo.wFormControl
            WHERE wFormId = @FormId AND isDeleted = 0 AND jsonId IS NOT NULL AND LTRIM(RTRIM(jsonId)) <> ''
            """;

        var list = new List<FormControlRow>();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new FormControlRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4)));
        }

        return list;
    }

    private sealed record FormControlRow(int Id, string JsonId, string? Name, string? Type, int ParentId);

    private sealed class PdfFormSchema
    {
        public static PdfFormSchema Empty { get; } = new([], null);

        private readonly Dictionary<string, string> _jsonIdToName;
        private readonly Dictionary<string, string> _columnJsonIdToTableName;
        private readonly Dictionary<string, string> _columnNameToTableName;
        private readonly Dictionary<string, string> _tableJsonIdToName;
        private readonly HashSet<string> _fileJsonIds;

        private PdfFormSchema(
            IReadOnlyList<FormControlRow> controls,
            FormJsonDto? formJson)
        {
            _jsonIdToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _columnJsonIdToTableName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _columnNameToTableName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _tableJsonIdToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _fileJsonIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var controlsById = controls.ToDictionary(static c => c.Id);
            foreach (var control in controls)
            {
                if (string.IsNullOrWhiteSpace(control.JsonId))
                    continue;

                var jsonId = control.JsonId.Trim();
                var displayName = ResolveControlName(control);
                if (!string.IsNullOrWhiteSpace(displayName) && !FormFieldNameResolver.LooksLikeJsonId(displayName))
                    _jsonIdToName[jsonId] = displayName;

                if (IsFileType(control.Type))
                    _fileJsonIds.Add(jsonId);

                if (string.Equals(control.Type, "TABLE", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(displayName)
                    && !FormFieldNameResolver.LooksLikeJsonId(displayName))
                {
                    _tableJsonIdToName[jsonId] = displayName;
                }

                if (control.ParentId > 0
                    && controlsById.TryGetValue(control.ParentId, out var parent)
                    && string.Equals(parent.Type, "TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    var tableName = ResolveControlName(parent);
                    if (!string.IsNullOrWhiteSpace(tableName) && !FormFieldNameResolver.LooksLikeJsonId(tableName))
                    {
                        _columnJsonIdToTableName[jsonId] = tableName;
                        if (!string.IsNullOrWhiteSpace(displayName) && !FormFieldNameResolver.LooksLikeJsonId(displayName))
                            _columnNameToTableName[displayName] = tableName;
                    }
                }
            }

            foreach (var (tableJsonId, tableLabel) in FormFieldNameResolver.BuildTableJsonIdToLabel(formJson))
                _tableJsonIdToName[tableJsonId] = tableLabel;

            foreach (var (jsonId, label) in FormFieldNameResolver.BuildJsonIdLabelIndex(formJson))
            {
                if (!_jsonIdToName.TryGetValue(jsonId, out var existing) || FormFieldNameResolver.LooksLikeJsonId(existing))
                    _jsonIdToName[jsonId] = label;
            }

            foreach (var (columnJsonId, tableLabel) in FormFieldNameResolver.BuildColumnJsonIdToTableLabel(formJson))
            {
                _columnJsonIdToTableName[columnJsonId] = tableLabel;
                if (_jsonIdToName.TryGetValue(columnJsonId, out var columnName)
                    && !FormFieldNameResolver.LooksLikeJsonId(columnName))
                {
                    _columnNameToTableName[columnName] = tableLabel;
                }
            }
        }

        public static PdfFormSchema From(IReadOnlyList<FormControlRow> controls, FormJsonDto? formJson) =>
            controls.Count == 0 && formJson == null ? Empty : new PdfFormSchema(controls, formJson);

        public string ResolveName(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return key;

            if (_jsonIdToName.TryGetValue(key.Trim(), out var mapped) && !string.IsNullOrWhiteSpace(mapped))
                return mapped.Trim();

            return key.Trim();
        }

        public string? ResolveTableName(string key)
        {
            var trimmed = key.Trim();
            if (_tableJsonIdToName.TryGetValue(trimmed, out var byJsonId))
                return byJsonId;

            var resolved = ResolveName(trimmed);
            return _tableJsonIdToName.Values.FirstOrDefault(
                name => string.Equals(name, resolved, StringComparison.OrdinalIgnoreCase));
        }

        public bool TryGetTableForColumn(string baseKey, string columnName, out string tableName)
        {
            if (_columnJsonIdToTableName.TryGetValue(baseKey.Trim(), out tableName!))
                return true;

            return _columnNameToTableName.TryGetValue(columnName.Trim(), out tableName!);
        }

        public bool IsTableColumnKey(string key)
        {
            if (TrySplitRowSuffix(key, out var baseKey, out _))
                return TryGetTableForColumn(baseKey, ResolveName(baseKey), out _);

            return _columnJsonIdToTableName.ContainsKey(key.Trim())
                || _columnNameToTableName.ContainsKey(ResolveName(key));
        }

        public bool IsFileField(string key) =>
            _fileJsonIds.Contains(key.Trim()) || _fileJsonIds.Contains(ResolveName(key));

        private static string ResolveControlName(FormControlRow control)
        {
            var name = control.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name) && !FormFieldNameResolver.LooksLikeJsonId(name))
                return name;

            return control.JsonId.Trim();
        }

        private static bool IsFileType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return false;

            return type.Contains("FILE", StringComparison.OrdinalIgnoreCase);
        }
    }
}
