using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Workflow.Application;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows.Commands.MoveToNextStep;
using SaaSApp.Workflow.Domain.Entities;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class WorkflowApAgentMoveNextService : IWorkflowApAgentMoveNextService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, string> RepositoryFieldAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceNo"] = "InvoiceNumber",
            ["Invoice No"] = "InvoiceNumber",
            ["PONumber"] = "PoNumber",
            ["PO Number"] = "PoNumber",
            ["InvoiceDate"] = "DocumentDate",
            ["Invoice Date"] = "DocumentDate",
            ["VendorName"] = "Supplier",
            ["Vendor Name"] = "Supplier",
            ["InvoiceAmount"] = "Amount",
            ["Invoice Amount"] = "Amount",
            ["Invoice Tax Amount"] = "InvoiceTaxAmount",
            ["currency"] = "Currency",
            ["Matter ID"] = "MatterId",
            ["GL Account"] = "GlAccount",
            ["GL Category"] = "GlCategory",
            ["Cost Center"] = "CostCenter",
            ["Matched Status"] = "MatchedStatus",
            ["TERMS"] = "Terms",
        };

    private static readonly string[] LineItemControlHints =
    [
        "lineitem", "lineitems", "invoiceline", "invoiceextracted", "extractedline", "invoice_line", "line item"
    ];

    private readonly ITenantContext _tenantContext;
    private readonly IWorkflowTableCreator _tableCreator;
    private readonly IRepositoryItemQueryService _repositoryItems;
    private readonly ILogger<WorkflowApAgentMoveNextService> _logger;

    public WorkflowApAgentMoveNextService(
        ITenantContext tenantContext,
        IWorkflowTableCreator tableCreator,
        IRepositoryItemQueryService repositoryItems,
        ILogger<WorkflowApAgentMoveNextService> logger)
    {
        _tenantContext = tenantContext;
        _tableCreator = tableCreator;
        _repositoryItems = repositoryItems;
        _logger = logger;
    }

    public Task SaveAgentValidationAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        WorkflowStep apAgentStep,
        Guid userId,
        MoveToNextStepApAgentPayload payload,
        int? legacyTransactionId,
        CancellationToken cancellationToken = default) =>
        InsertAgentValidationAsync(
            workflowId,
            workflowInstanceId,
            apAgentStep,
            userId,
            payload,
            legacyTransactionId,
            cancellationToken);

    public async Task BindEzfbFromRepositoryAsync(
        Guid tenantId,
        MoveToNextStepApAgentPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload.FormId)
            || !payload.FormEntryId.HasValue
            || !payload.RepositoryId.HasValue
            || !payload.RepositoryItemId.HasValue)
        {
            _logger.LogDebug("Skipping ezfb bind: form, entry, repository, or item id missing.");
            return;
        }

        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var formId = FormIdNaming.NormalizeFormId(payload.FormId);
        var wFormIdValue = await ResolveWFormIdParameterAsync(connection, formId, cancellationToken);
        var controls = await LoadFormControlsAsync(connection, wFormIdValue, cancellationToken);
        if (controls.Count == 0)
            return;

        var itemsTable = await ResolveItemsTableNameAsync(connection, tenantId, payload.RepositoryId.Value, cancellationToken);
        if (itemsTable == null)
            return;

        var repoValues = await LoadRepositoryItemValuesAsync(
            connection, itemsTable, tenantId, payload.RepositoryId.Value, payload.RepositoryItemId.Value, cancellationToken);
        if (repoValues.Count == 0)
            return;

        var agentValues = ParseAgentFieldValues(payload.AiAgentResponseJson);
        var tableSuffix = FormIdNaming.GetEzfbTableSuffix(formId);
        var ezfbTable = $"dbo.[ezfb_{tableSuffix}_items]";
        var ezfbColumns = await LoadTableColumnsAsync(connection, "dbo", $"ezfb_{tableSuffix}_items", cancellationToken);
        if (ezfbColumns.Count == 0)
            return;

        var entryId = payload.FormEntryId.Value;
        foreach (var control in controls)
        {
            var jsonId = control.JsonId;
            if (!TryResolveEzfbColumn(jsonId, ezfbColumns, out var col))
                continue;

            var current = await GetEzfbColumnValueAsync(connection, ezfbTable, entryId, col, cancellationToken);
            if (!IsEmptyValue(current))
                continue;

            if (!TryResolveValue(jsonId, control.Name, repoValues, agentValues, out var newValue) || IsEmptyValue(newValue))
                continue;

            await UpdateEzfbColumnAsync(connection, ezfbTable, entryId, col, newValue!, cancellationToken);
        }
    }

    public async Task<ApAgentMetadataApplyResult> ApplyMetadataAsync(
        Guid tenantId,
        ApAgentMetadataApplyRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ApplyMetadataCoreAsync(tenantId, request, userId, cancellationToken);
        }
        catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
        {
            _logger.LogError(ex, "AP agent metadata failed for item {ItemId}, formEntryId {FormEntryId}", request.ItemId, request.FormEntryId);
            throw new InvalidOperationException($"AP agent metadata failed: {ex.Message}", ex);
        }
    }

    public async Task<int> ApplyFormDataToEzfbAsync(
        string formId,
        int formEntryId,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formId))
            throw new ArgumentException("formId is required.");
        if (formEntryId <= 0)
            throw new ArgumentException("formEntryId must be a positive integer.");
        if (fields.Count == 0)
            return 0;

        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Tenant connection string not resolved.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var normalizedFormId = FormIdNaming.NormalizeFormId(formId);
        var wFormIdValue = await ResolveWFormIdParameterAsync(connection, normalizedFormId, cancellationToken);
        var controls = await LoadFormControlsAsync(connection, wFormIdValue, cancellationToken);
        var tableSuffix = FormIdNaming.GetEzfbTableSuffix(normalizedFormId);
        var ezfbTable = $"dbo.[ezfb_{tableSuffix}_items]";
        var ezfbColumns = await LoadTableColumnsAsync(connection, "dbo", $"ezfb_{tableSuffix}_items", cancellationToken);

        if (ezfbColumns.Count == 0)
        {
            _logger.LogWarning("Table {EzfbTable} not found or has no columns; formData ezfb update skipped.", ezfbTable);
            return 0;
        }

        var updated = 0;
        foreach (var (key, value) in fields)
        {
            if (IsEmptyValue(value))
                continue;

            string? column = null;
            if (TryResolveControlForField(key, controls, out var control) && control is not null)
            {
                if (!TryResolveEzfbColumn(control.JsonId, ezfbColumns, out var colFromControl))
                {
                    _logger.LogWarning(
                        "ezfb column not found for jsonId {JsonId} (control name {ControlName}).",
                        control.JsonId,
                        control.Name);
                    continue;
                }

                column = colFromControl;
            }
            else if (!TryResolveEzfbColumn(key, ezfbColumns, out var colFromKey))
            {
                _logger.LogDebug("ezfb column not resolved for formData key {FieldKey}.", key);
                continue;
            }
            else
            {
                column = colFromKey;
            }

            try
            {
                await UpdateEzfbColumnAsync(connection, ezfbTable, formEntryId, column, value, cancellationToken);
                updated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "ezfb formData update failed for formEntryId {FormEntryId}, column {Column}, key {FieldKey}",
                    formEntryId,
                    column,
                    key);
            }
        }

        return updated;
    }

    private async Task<ApAgentMetadataApplyResult> ApplyMetadataCoreAsync(
        Guid tenantId,
        ApAgentMetadataApplyRequest request,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FormId))
            throw new ArgumentException("formId is required.");
        if (request.FormEntryId <= 0)
            throw new ArgumentException("formEntryId must be a positive integer.");
        if (request.Fields.Count == 0 && string.IsNullOrWhiteSpace(request.LineItemsJson))
            throw new ArgumentException("At least one field or lineItems is required.");

        var repositoryFields = BuildRepositoryMetadataFields(request.Fields, request.LineItemsJson);
        var repoFieldsUpdated = 0;
        try
        {
            var repoUpdate = await _repositoryItems.UpdateItemMetadataAsync(
                request.RepositoryId,
                tenantId,
                request.ItemId,
                repositoryFields,
                userId,
                cancellationToken);

            if (repoUpdate == null)
                throw new InvalidOperationException("Repository item not found.");

            repoFieldsUpdated = repoUpdate.UpdatedFieldCount;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("No valid metadata fields", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                ex,
                "Repository metadata skipped for item {ItemId}: no matching repository columns.",
                request.ItemId);
        }

        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Tenant connection string not resolved.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var formId = FormIdNaming.NormalizeFormId(request.FormId);
        var wFormIdValue = await ResolveWFormIdParameterAsync(connection, formId, cancellationToken);
        var controls = await LoadFormControlsAsync(connection, wFormIdValue, cancellationToken);
        var tableSuffix = FormIdNaming.GetEzfbTableSuffix(formId);
        var ezfbTable = $"dbo.[ezfb_{tableSuffix}_items]";
        var ezfbColumns = await LoadTableColumnsAsync(connection, "dbo", $"ezfb_{tableSuffix}_items", cancellationToken);

        var ezfbUpdated = 0;
        if (controls.Count == 0)
            _logger.LogWarning("No wFormControl rows for form {FormId}; ezfb metadata skipped.", request.FormId);
        else if (ezfbColumns.Count == 0)
            _logger.LogWarning("Table {EzfbTable} not found or has no columns; ezfb metadata skipped.", ezfbTable);

        if (controls.Count > 0 && ezfbColumns.Count > 0)
        {
            foreach (var (key, value) in request.Fields)
            {
                if (IsEmptyValue(value))
                    continue;
                if (ApAgentMetadataParser.IsLineItemSectionName(key))
                    continue;
                if (!TryResolveControlForField(key, controls, out var control) || control is null)
                    continue;

                if (!TryResolveEzfbColumn(control.JsonId, ezfbColumns, out var col))
                {
                    _logger.LogWarning(
                        "ezfb column not found for jsonId {JsonId} (control name {ControlName}).",
                        control.JsonId,
                        control.Name);
                    continue;
                }

                try
                {
                    await UpdateEzfbColumnAsync(connection, ezfbTable, request.FormEntryId, col, value, cancellationToken);
                    ezfbUpdated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "ezfb update failed for formEntryId {FormEntryId}, column {Column}, key {FieldKey}",
                        request.FormEntryId,
                        col,
                        key);
                }
            }
        }

        string? lineItemsColumn = null;
        var lineItemsUpdated = false;
        if (!string.IsNullOrWhiteSpace(request.LineItemsJson) && ezfbColumns.Count > 0)
        {
            lineItemsColumn = ResolveLineItemsEzfbColumn(controls, ezfbColumns);
            if (lineItemsColumn != null)
            {
                try
                {
                    await UpdateEzfbColumnAsync(
                        connection, ezfbTable, request.FormEntryId, lineItemsColumn, request.LineItemsJson, cancellationToken);
                    lineItemsUpdated = true;
                    ezfbUpdated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "ezfb line items update failed for formEntryId {FormEntryId}, column {Column}",
                        request.FormEntryId,
                        lineItemsColumn);
                }
            }
            else
            {
                _logger.LogWarning(
                    "No ezfb line-item column resolved (control name e.g. 'invoice extracted line item'). Form {FormId}.",
                    request.FormId);
            }
        }

        return new ApAgentMetadataApplyResult(
            request.ItemId,
            request.FormEntryId,
            repoFieldsUpdated,
            ezfbUpdated,
            lineItemsUpdated,
            lineItemsColumn);
    }

    public async Task AfterApAgentApproveAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        WorkflowStep apAgentStep,
        Guid tenantId,
        Guid userId,
        MoveToNextStepApAgentPayload payload,
        int? legacyTransactionId,
        CancellationToken cancellationToken = default)
    {
        await BindEzfbFromRepositoryAsync(tenantId, payload, cancellationToken);
        await SaveAgentValidationAsync(
            workflowId, workflowInstanceId, apAgentStep, userId, payload, legacyTransactionId, cancellationToken);
    }

    private async Task InsertAgentValidationAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        WorkflowStep apAgentStep,
        Guid userId,
        MoveToNextStepApAgentPayload payload,
        int? legacyTransactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.AiAgentResponseJson) && string.IsNullOrWhiteSpace(payload.AiAgentHtml))
            return;

        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await _tableCreator.EnsureAgentDataValidationTableAsync(workflowId, connectionString, cancellationToken);

        var suffix = workflowId.ToString("N")[..8];
        var table = $"workflow.[agentDataValidation_{suffix}]";
        // Legacy column ProcessId stores workflow instance id (engine sends instanceId, not processId).
        var processId = payload.InstanceId ?? workflowInstanceId;
        var transactionId = payload.TransactionId
            ?? legacyTransactionId?.ToString(CultureInfo.InvariantCulture)
            ?? string.Empty;

        var sql = $@"
INSERT INTO {table}
    (WorkflowId, ProcessId, TransactionId, Type, AgentResponse, AgentHtmlResponse,
     CreatedAt, CreatedBy, IsDeleted)
VALUES
    (@WorkflowId, @ProcessId, @TransactionId, @Type, @AgentResponse, @AgentHtmlResponse,
     SYSUTCDATETIME(), @CreatedBy, 0);";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
        cmd.Parameters.AddWithValue("@ProcessId", processId);
        cmd.Parameters.AddWithValue("@TransactionId", transactionId);
        cmd.Parameters.AddWithValue("@Type", apAgentStep.StageType ?? "AP_AGENT");
        cmd.Parameters.AddWithValue("@AgentResponse", (object?)payload.AiAgentResponseJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AgentHtmlResponse", (object?)payload.AiAgentHtml ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
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

    private sealed record FormControlRow(string JsonId, string? Name, string? Type);

    private static async Task<List<FormControlRow>> LoadFormControlsAsync(
        SqlConnection connection,
        object wFormIdValue,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT jsonId, name, type
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
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return list;
    }

    private static async Task<string?> ResolveItemsTableNameAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 1 ItemsTableName
            FROM repository.Repositories
            WHERE Id = @RepositoryId AND TenantId = @TenantId AND IsDeleted = 0
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        var name = (await cmd.ExecuteScalarAsync(cancellationToken)) as string;
        if (!string.IsNullOrWhiteSpace(name)
            && Regex.IsMatch(name, @"^Items_[a-f0-9]{8}$", RegexOptions.IgnoreCase))
            return name;

        return $"Items_{repositoryId.ToString("N")[..8]}";
    }

    private static async Task<Dictionary<string, string?>> LoadRepositoryItemValuesAsync(
        SqlConnection connection,
        string itemsTableName,
        Guid tenantId,
        Guid repositoryId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT *
FROM repository.[{itemsTableName}]
WHERE Id = @ItemId AND TenantId = @TenantId AND RepositoryId = @RepositoryId AND IsDeleted = 0";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            dict[name] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
        }

        return dict;
    }

    private static Dictionary<string, string?> ParseAgentFieldValues(string? json)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return dict;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return dict;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("AIAGENTResponse is not valid JSON.", ex);
        }

        return dict;
    }

    private static bool TryResolveValue(
        string jsonId,
        string? controlName,
        IReadOnlyDictionary<string, string?> repository,
        IReadOnlyDictionary<string, string?> agent,
        out string? value)
    {
        value = null;
        var candidates = new List<string> { jsonId };
        if (TryToEzfbColumnName(jsonId, out var ezfbCol))
            candidates.Add(ezfbCol);
        if (!string.IsNullOrWhiteSpace(controlName))
            candidates.Add(controlName.Trim());
        if (RepositoryFieldAliases.TryGetValue(jsonId, out var alias))
            candidates.Add(alias);

        foreach (var key in candidates)
        {
            if (repository.TryGetValue(key, out var repoVal) && !IsEmptyValue(repoVal))
            {
                value = repoVal;
                return true;
            }
        }

        foreach (var key in candidates)
        {
            if (agent.TryGetValue(key, out var agentVal) && !IsEmptyValue(agentVal))
            {
                value = agentVal;
                return true;
            }
        }

        return false;
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
        if (TryMatchControlByKeyOrJsonId(key, controls, out control))
            return true;

        if (RepositoryFieldAliases.TryGetValue(key, out var alias)
            && !string.Equals(alias, key, StringComparison.OrdinalIgnoreCase))
        {
            return TryMatchControlByKeyOrJsonId(alias, controls, out control);
        }

        return false;
    }

    private static bool TryMatchControlByKeyOrJsonId(
        string key,
        IReadOnlyList<FormControlRow> controls,
        out FormControlRow? control)
    {
        control = null;
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

    private static string? ResolveLineItemsEzfbColumn(
        IReadOnlyList<FormControlRow> controls,
        IReadOnlySet<string> ezfbColumns)
    {
        foreach (var row in controls)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
                continue;

            var name = row.Name.Trim();
            if (ApAgentMetadataParser.IsLineItemSectionName(name) || MatchesLineItemHint(name))
            {
                if (TryResolveEzfbColumn(row.JsonId, ezfbColumns, out var exactCol))
                    return exactCol;
            }
        }

        foreach (var row in controls)
        {
            if (!MatchesLineItemHint(row.JsonId) && !MatchesLineItemHint(row.Name) && !MatchesLineItemHint(row.Type))
                continue;

            if (TryResolveEzfbColumn(row.JsonId, ezfbColumns, out var col))
                return col;
        }

        foreach (var col in ezfbColumns)
        {
            if (MatchesLineItemHint(col))
                return col;
        }

        return null;
    }

    private static bool MatchesLineItemHint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .ToLowerInvariant();
        return LineItemControlHints.Any(h => normalized.Contains(h, StringComparison.Ordinal));
    }

    private static Dictionary<string, string> BuildRepositoryMetadataFields(
        IReadOnlyDictionary<string, string> fields,
        string? lineItemsJson)
    {
        var dict = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(lineItemsJson))
            return dict;

        if (!dict.ContainsKey("OcrJson"))
            dict["OcrJson"] = lineItemsJson.StartsWith('{')
                ? lineItemsJson
                : $"{{\"lineItems\":{lineItemsJson}}}";

        return dict;
    }

    private static async Task<HashSet<string>> LoadTableColumnsAsync(
        SqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table
            """;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            set.Add(reader.GetString(0));
        return set;
    }

    private static async Task<string?> GetEzfbColumnValueAsync(
        SqlConnection connection,
        string ezfbTable,
        int itemId,
        string column,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT [{column.Replace("]", "]]")}] FROM {ezfbTable} WHERE itemId = @ItemId";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        var o = await cmd.ExecuteScalarAsync(cancellationToken);
        return o == null || o == DBNull.Value ? null : Convert.ToString(o, CultureInfo.InvariantCulture);
    }

    private async Task UpdateEzfbColumnAsync(
        SqlConnection connection,
        string ezfbTable,
        int itemId,
        string column,
        string value,
        CancellationToken cancellationToken)
    {
        var sql = $@"
UPDATE {ezfbTable}
SET [{column.Replace("]", "]]")}] = @Value,
    modifiedAt = CONVERT(NVARCHAR(50), SYSUTCDATETIME(), 127)
WHERE itemId = @ItemId AND (isDeleted = 0 OR isDeleted IS NULL);
SELECT CAST(@@ROWCOUNT AS int);";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Value", value);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        if (rows == 0)
        {
            _logger.LogWarning(
                "ezfb UPDATE affected 0 rows for table {Table}, itemId {ItemId}, column {Column}",
                ezfbTable,
                itemId,
                column);
        }
    }

    private static bool IsEmptyValue(string? value) =>
        string.IsNullOrWhiteSpace(value);

    private static string SanitizeColumnName(string name)
    {
        var cleaned = Regex.Replace(name.Trim(), @"[^a-zA-Z0-9_]", "");
        if (cleaned.Length == 0)
            throw new ArgumentException($"Invalid field name: {name}");
        if (char.IsDigit(cleaned[0]))
            cleaned = "F_" + cleaned;
        return cleaned;
    }

    /// <summary>Same rules as FormService.EscapeSqlIdentifier — ezfb columns keep jsonId as-is (including leading digits).</summary>
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

        // Legacy: older code used F_ prefix for leading-digit jsonIds.
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
