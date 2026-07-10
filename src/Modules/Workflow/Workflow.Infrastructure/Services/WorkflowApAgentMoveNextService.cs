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
            ["Invoice Number"] = "InvoiceNumber",
            ["PONumber"] = "PoNumber",
            ["PO Number"] = "PoNumber",
            ["PO No"] = "PoNumber",
            ["PONumber"] = "PoNumber",
            ["InvoiceDate"] = "DocumentDate",
            ["Invoice Date"] = "DocumentDate",
            ["PODate"] = "PoDate",
            ["PO Date"] = "PoDate",
            ["PO DATE"] = "PoDate",
            ["VendorName"] = "Supplier",
            ["Vendor Name"] = "Supplier",
            ["Vendor"] = "Supplier",
            ["InvoiceAmount"] = "Amount",
            ["Invoice Amount"] = "Amount",
            ["POAmount"] = "PoAmount",
            ["PO Amount"] = "PoAmount",
            ["Invoice Tax Amount"] = "InvoiceTaxAmount",
            ["InvoiceTaxAmount"] = "InvoiceTaxAmount",
            ["currency"] = "Currency",
            ["Matter ID"] = "MatterId",
            ["GL Account"] = "GlAccount",
            ["GL Category"] = "GlCategory",
            ["Cost Center"] = "CostCenter",
            ["Matched Status"] = "MatchedStatus",
            ["TERMS"] = "Terms",
            ["Terms"] = "Terms",
            ["Buyer Name"] = "Buyer",
            ["Supplier Address"] = "SupplierAddress",
            ["Vendor Address"] = "SupplierAddress",
            ["VendorAddress"] = "SupplierAddress",
            ["Ship To Address"] = "ShipToAddress",
            ["Ship-To Address"] = "ShipToAddress",
            ["ShipToAddress"] = "ShipToAddress",
            ["Pay To Address"] = "PayToAddress",
            ["Pay-To Address"] = "PayToAddress",
            ["PayToAddress"] = "PayToAddress",
            ["Document Type"] = "DocumentType",
            ["DocumentType"] = "DocumentType",
            ["decision"] = "MatchedStatus",
            ["Gross Amount"] = "Gross Amount",
        };

    /// <summary>po_row audit/system columns — never written to ezfb form.</summary>
    private static readonly HashSet<string> PoRowMetadataFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "itemId", "createdAt", "modifiedAt", "createdBy", "modifiedBy", "isDeleted", "todayTask", "isMarked"
    };

    /// <summary>po_row business keys → alternate wFormControl names to try when matching form fields.</summary>
    private static readonly Dictionary<string, string[]> PoRowFormFieldMatchCandidates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PO Number"] = ["PO Number", "PoNumber", "PONumber"],
            ["PO Amount"] = ["PO Amount", "PoAmount", "POAmount", "Gross Amount", "Amount"],
            ["Gross Amount"] = ["Gross Amount", "PO Amount", "PoAmount", "POAmount", "Amount"],
            ["Vendor"] = ["Vendor", "Vendor Name", "Supplier"],
            ["Vendor Name"] = ["Vendor Name", "Vendor", "Supplier"],
            ["PO Date"] = ["PO Date", "PO DATE", "DocumentDate"],
            ["Bill-To Address"] = ["Bill-To Address", "Bill To Address"],
            ["Ship-To Address"] = ["Ship-To Address", "Ship To Address"],
            ["Terms Descr"] = ["Terms Descr", "Terms", "TERMS"],
            ["Buyer Name"] = ["Buyer Name", "Buyer"],
            ["Notes"] = ["Notes"],
            ["PODetail"] = ["PODetail", "PO Detail"],
            ["PO Line Item Mapped"] = ["PO Line Item Mapped", "PO TABLE", "PO Table"],
        };

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
        string? lineItemsJson = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formId))
            throw new ArgumentException("formId is required.");
        if (formEntryId <= 0)
            throw new ArgumentException("formEntryId must be a positive integer.");
        if (fields.Count == 0 && string.IsNullOrWhiteSpace(lineItemsJson))
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
            FormControlRow? matchedControl = null;
            if (TryResolveControlForField(key, controls, out var control) && control is not null)
            {
                matchedControl = control;
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

            var valueToWrite = value;
            if (IsJsonArrayValue(value)
                && matchedControl is not null
                && IsLineItemControl(matchedControl))
            {
                valueToWrite = NormalizeLineItemsJson(value);
            }
            else if (IsJsonArrayValue(value) && matchedControl is null)
            {
                _logger.LogDebug(
                    "Skipping array formData key {FieldKey}: no matching wFormControl for DYNAMIC_TABLE.",
                    key);
                continue;
            }

            try
            {
                await UpdateEzfbColumnAsync(connection, ezfbTable, formEntryId, column, valueToWrite, cancellationToken);
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

        if (!string.IsNullOrWhiteSpace(lineItemsJson) && ezfbColumns.Count > 0)
        {
            var lineItemsColumn = ResolveLineItemsEzfbColumn(controls, ezfbColumns);
            if (lineItemsColumn != null)
            {
                try
                {
                    await UpdateEzfbColumnAsync(
                        connection,
                        ezfbTable,
                        formEntryId,
                        lineItemsColumn,
                        NormalizeLineItemsJson(lineItemsJson),
                        cancellationToken);
                    updated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "ezfb line items update failed for formEntryId {FormEntryId}, column {Column}",
                        formEntryId,
                        lineItemsColumn);
                }
            }
            else
            {
                _logger.LogWarning(
                    "No ezfb DYNAMIC_TABLE / line-item column resolved for form {FormId}.",
                    formId);
            }
        }

        return updated;
    }

    public async Task<int> ApplyPoRowFromStoredAgentValidationAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        string formId,
        int formEntryId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(formId) || formEntryId <= 0)
                return 0;

            var agentResponseJson = await LoadLatestAgentResponseAsync(workflowId, workflowInstanceId, cancellationToken);
            if (string.IsNullOrWhiteSpace(agentResponseJson))
            {
                _logger.LogDebug(
                    "No agent validation row for workflow {WorkflowId}, instance {InstanceId}; po_row form sync skipped.",
                    workflowId,
                    workflowInstanceId);
                return 0;
            }

            var poRowFields = ExtractPoRowFields(agentResponseJson);
            if (poRowFields.Count == 0)
            {
                _logger.LogDebug(
                    "Agent response has no po_row fields for instance {InstanceId}; form sync skipped.",
                    workflowInstanceId);
                return 0;
            }

            var updated = await ApplyPoRowFieldsToEzfbAsync(formId, formEntryId, poRowFields, cancellationToken);
            _logger.LogInformation(
                "Applied {UpdatedCount} po_row field(s) from agentDataValidation to form {FormId} entry {FormEntryId} for instance {InstanceId}.",
                updated,
                formId,
                formEntryId,
                workflowInstanceId);
            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "po_row form sync skipped for instance {InstanceId}; move-next transaction was not affected.",
                workflowInstanceId);
            return 0;
        }
    }

    private async Task<string?> LoadLatestAgentResponseAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken)
    {
        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        await _tableCreator.EnsureAgentDataValidationTableAsync(workflowId, connectionString, cancellationToken);

        var suffix = workflowId.ToString("N")[..8];
        var table = $"workflow.[agentDataValidation_{suffix}]";
        var sql = $@"
SELECT TOP 1 AgentResponse
FROM {table}
WHERE IsDeleted = 0
  AND ProcessId = @ProcessId
ORDER BY CreatedAt DESC, Id DESC;";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ProcessId", workflowInstanceId);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result == null || result == DBNull.Value ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
    }

    internal static Dictionary<string, string> ExtractPoRowFields(string agentResponseJson)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(agentResponseJson))
            return fields;

        try
        {
            using var doc = JsonDocument.Parse(agentResponseJson);
            if (!TryGetPoRowObject(doc.RootElement, out var poRow))
            {
                return fields;
            }

            foreach (var prop in poRow.EnumerateObject())
            {
                if (PoRowMetadataFields.Contains(prop.Name))
                    continue;

                if (prop.Value.ValueKind == JsonValueKind.Null)
                    continue;

                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Array or JsonValueKind.Object => prop.Value.GetRawText(),
                    _ => prop.Value.GetRawText()
                };

                if (!IsEmptyValue(value))
                    fields[prop.Name] = value!;
            }
        }
        catch (JsonException)
        {
            // Invalid stored agent JSON should not block move-next.
            return fields;
        }

        return fields;
    }

    private static bool TryGetPoRowObject(JsonElement root, out JsonElement poRow)
    {
        poRow = default;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (root.TryGetProperty("po_row", out poRow) && poRow.ValueKind == JsonValueKind.Object)
            return true;

        foreach (var prop in root.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "AIAGENTResponse", StringComparison.OrdinalIgnoreCase))
                continue;

            if (prop.Value.ValueKind == JsonValueKind.Object
                && prop.Value.TryGetProperty("po_row", out poRow)
                && poRow.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            break;
        }

        return false;
    }

    private async Task<int> ApplyPoRowFieldsToEzfbAsync(
        string formId,
        int formEntryId,
        IReadOnlyDictionary<string, string> poRowFields,
        CancellationToken cancellationToken)
    {
        if (poRowFields.Count == 0)
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
            _logger.LogWarning("Table {EzfbTable} not found; po_row ezfb update skipped.", ezfbTable);
            return 0;
        }

        var updated = 0;
        foreach (var (poRowKey, value) in poRowFields)
        {
            if (IsEmptyValue(value))
                continue;

            if (!TryResolveControlForPoRowField(poRowKey, controls, out var control) || control is null)
            {
                _logger.LogDebug("No form control match for po_row key {PoRowKey}.", poRowKey);
                continue;
            }

            if (!TryResolveEzfbColumn(control.JsonId, ezfbColumns, out var column))
            {
                _logger.LogWarning(
                    "ezfb column not found for po_row key {PoRowKey}, jsonId {JsonId}, control {ControlName}.",
                    poRowKey,
                    control.JsonId,
                    control.Name);
                continue;
            }

            var valueToWrite = value;
            if (IsJsonArrayValue(value) && IsPoRowLineItemKey(poRowKey) && IsDynamicTableControl(control))
                valueToWrite = NormalizeLineItemsJson(value);
            else if (IsJsonArrayValue(value))
                continue;

            try
            {
                await UpdateEzfbColumnAsync(connection, ezfbTable, formEntryId, column, valueToWrite, cancellationToken);
                updated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "po_row ezfb update failed for formEntryId {FormEntryId}, po_row key {PoRowKey}, column {Column}",
                    formEntryId,
                    poRowKey,
                    column);
            }
        }

        return updated;
    }

    private static bool IsPoRowLineItemKey(string poRowKey) =>
        string.Equals(poRowKey, "PO Line Item Mapped", StringComparison.OrdinalIgnoreCase)
        || string.Equals(poRowKey, "PODetail", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveControlForPoRowField(
        string poRowKey,
        IReadOnlyList<FormControlRow> controls,
        out FormControlRow? control)
    {
        control = null;
        if (string.IsNullOrWhiteSpace(poRowKey))
            return false;

        var candidates = new List<string> { poRowKey.Trim() };
        if (PoRowFormFieldMatchCandidates.TryGetValue(poRowKey, out var extras))
            candidates.AddRange(extras);
        if (RepositoryFieldAliases.TryGetValue(poRowKey, out var repoAlias))
            candidates.Add(repoAlias);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryResolveControlForField(candidate, controls, out control))
                return true;
        }

        return false;
    }

    private static bool IsJsonArrayValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed.StartsWith("[", StringComparison.Ordinal)
            && trimmed.EndsWith("]", StringComparison.Ordinal);
    }

    private static string NormalizeLineItemsJson(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("[", StringComparison.Ordinal)
            ? trimmed
            : value;
    }

    private static bool IsLineItemControl(FormControlRow control) =>
        IsInvoiceExtractedLineItemControl(control);

    private static bool IsInvoiceExtractedLineItemControl(FormControlRow control)
    {
        if (!IsDynamicTableControl(control))
            return false;

        return ApAgentMetadataParser.IsLineItemSectionName(control.Name ?? string.Empty);
    }

    private static bool IsDynamicTableControl(FormControlRow control) =>
        !string.IsNullOrWhiteSpace(control.Type)
        && control.Type.Contains("DYNAMIC_TABLE", StringComparison.OrdinalIgnoreCase);

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
            var updatedEzfbColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

                if (!updatedEzfbColumns.Add(col))
                    continue;

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
        await SaveAgentValidationAsync(
            workflowId, workflowInstanceId, apAgentStep, userId, payload, legacyTransactionId, cancellationToken);
        await TryApplyRepositoryFieldsFromAgentResponseAsync(
            workflowId,
            workflowInstanceId,
            tenantId,
            userId,
            payload,
            cancellationToken);
        await BindEzfbFromRepositoryAsync(tenantId, payload, cancellationToken);
        await TryApplyDecisionToMatchedStatusFormAsync(tenantId, payload, cancellationToken);
    }

    /// <summary>Writes AIAGENTResponse.decision to the form Matched Status control (jsonId column). Does not touch po_row sync.</summary>
    private async Task TryApplyDecisionToMatchedStatusFormAsync(
        Guid tenantId,
        MoveToNextStepApAgentPayload payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.AiAgentResponseJson)
            || string.IsNullOrWhiteSpace(payload.FormId)
            || !payload.FormEntryId.HasValue)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload.AiAgentResponseJson);
            if (!TryGetAgentResponseRoot(doc.RootElement, out var root))
                return;

            if (!root.TryGetProperty("decision", out var decisionProp))
                return;

            var decision = JsonElementToString(decisionProp);
            if (IsEmptyValue(decision))
                return;

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

            var tableSuffix = FormIdNaming.GetEzfbTableSuffix(formId);
            var ezfbTable = $"dbo.[ezfb_{tableSuffix}_items]";
            var ezfbColumns = await LoadTableColumnsAsync(connection, "dbo", $"ezfb_{tableSuffix}_items", cancellationToken);
            if (ezfbColumns.Count == 0)
                return;

            if (!TryResolveControlForField("Matched Status", controls, out var control) || control is null)
            {
                _logger.LogDebug(
                    "No wFormControl match for Matched Status on form {FormId}, entry {FormEntryId}.",
                    payload.FormId,
                    payload.FormEntryId.Value);
                return;
            }

            if (!TryResolveEzfbColumn(control.JsonId, ezfbColumns, out var column))
            {
                _logger.LogWarning(
                    "ezfb column not found for Matched Status jsonId {JsonId} on form {FormId}.",
                    control.JsonId,
                    payload.FormId);
                return;
            }

            await UpdateEzfbColumnAsync(
                connection,
                ezfbTable,
                payload.FormEntryId.Value,
                column,
                decision!,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Matched Status form update from decision failed for form {FormId} entry {FormEntryId}.",
                payload.FormId,
                payload.FormEntryId);
        }
    }

    private async Task TryApplyRepositoryFieldsFromAgentResponseAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        Guid tenantId,
        Guid userId,
        MoveToNextStepApAgentPayload payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.AiAgentResponseJson)
            || !payload.RepositoryId.HasValue
            || !payload.RepositoryItemId.HasValue)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload.AiAgentResponseJson);
            if (!TryGetAgentResponseRoot(doc.RootElement, out var root))
                return;

            Dictionary<string, string> fields;
            try
            {
                var (parsedFields, _) = ApAgentMetadataParser.ParseFieldsPayload(root);
                fields = new Dictionary<string, string>(parsedFields, StringComparer.OrdinalIgnoreCase);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "AIAGENTResponse field parse failed for repository sync on instance {InstanceId}; using po_row fallback.",
                    workflowInstanceId);
                fields = new Dictionary<string, string>(ExtractPoRowFields(payload.AiAgentResponseJson), StringComparer.OrdinalIgnoreCase);
            }

            if (root.TryGetProperty("decision", out var decisionProp)
                && !IsEmptyValue(JsonElementToString(decisionProp)))
            {
                fields["Matched Status"] = JsonElementToString(decisionProp)!;
            }

            EnrichInvoiceFieldsFromAgentResponse(root, fields);
            EnsureApInvoiceDocumentType(fields);

            if (fields.Count == 0)
                return;

            await _repositoryItems.UpdateItemMetadataAsync(
                payload.RepositoryId.Value,
                tenantId,
                payload.RepositoryItemId.Value,
                fields,
                userId,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AIAGENTResponse is not valid JSON for repository sync on instance {InstanceId}.", workflowInstanceId);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("No valid metadata fields", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Repository field sync skipped for instance {InstanceId}: no matching repository columns.", workflowInstanceId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogWarning(ex, "AIAGENTResponse repository sync failed for instance {InstanceId}.", workflowInstanceId);
        }
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
                if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    continue;

                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => null
                };
            }
        }
        catch (JsonException)
        {
            // Invalid agent JSON must not block move-next.
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
        if (EzfbColumnNaming.TryToColumnName(jsonId, out var ezfbCol))
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

        foreach (var reverseAlias in GetReverseAliasCandidates(key))
        {
            if (TryMatchControlByKeyOrJsonId(reverseAlias, controls, out control))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> GetReverseAliasCandidates(string key)
    {
        foreach (var (aliasKey, canonical) in RepositoryFieldAliases)
        {
            if (string.Equals(canonical, key, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(aliasKey, key, StringComparison.OrdinalIgnoreCase))
            {
                yield return aliasKey;
            }
        }
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

            if (EzfbColumnNaming.TryToColumnName(row.JsonId, out var ezfbCol)
                && string.Equals(ezfbCol, key, StringComparison.OrdinalIgnoreCase))
            {
                control = row;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves ezfb column for AP-agent / metadata invoice line items.
    /// Targets <c>DYNAMIC_TABLE</c> controls such as "Invoice Extracted Line Item" (not PO TABLE controls).
    /// </summary>
    private static string? ResolveLineItemsEzfbColumn(
        IReadOnlyList<FormControlRow> controls,
        IReadOnlySet<string> ezfbColumns)
    {
        foreach (var row in controls)
        {
            if (!IsInvoiceExtractedLineItemControl(row))
                continue;

            if (TryResolveEzfbColumn(row.JsonId, ezfbColumns, out var col))
                return col;
        }

        FormControlRow? onlyDynamic = null;
        var dynamicCount = 0;
        foreach (var row in controls)
        {
            if (!IsDynamicTableControl(row))
                continue;

            dynamicCount++;
            onlyDynamic = row;
            if (dynamicCount > 1)
                break;
        }

        if (dynamicCount == 1
            && onlyDynamic is not null
            && TryResolveEzfbColumn(onlyDynamic.JsonId, ezfbColumns, out var singleCol))
        {
            return singleCol;
        }

        return null;
    }

    /// <summary>
    /// When AP agent sends the same value as PO Number, PONumber, and PoNumber, keep one canonical key for repository SQL.
    /// </summary>
    private static void CollapseRepositoryFieldAliases(Dictionary<string, string> fields)
    {
        foreach (var (aliasKey, canonical) in RepositoryFieldAliases)
        {
            if (string.Equals(aliasKey, canonical, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!fields.TryGetValue(aliasKey, out var value))
                continue;

            if (!fields.ContainsKey(canonical) || string.IsNullOrWhiteSpace(fields[canonical]))
                fields[canonical] = value;

            fields.Remove(aliasKey);
        }
    }

    private static Dictionary<string, string> BuildRepositoryMetadataFields(
        IReadOnlyDictionary<string, string> fields,
        string? lineItemsJson)
    {
        var dict = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
        CollapseRepositoryFieldAliases(dict);
        EnsureApInvoiceDocumentType(dict);
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

    private static void EnrichInvoiceFieldsFromAgentResponse(JsonElement root, Dictionary<string, string> fields)
    {
        if (!HasValue(fields, "InvoiceNo", "Invoice No", "InvoiceNumber")
            && TryGetNestedString(root, out var invoiceNo, "Extracted Invoice JSON", "invoice_header", "Invoice No"))
        {
            fields["InvoiceNo"] = invoiceNo!;
            fields["Invoice No"] = invoiceNo!;
        }

        if (!HasValue(fields, "InvoiceAmount", "Invoice Amount", "Amount")
            && TryGetNestedString(root, out var invoiceAmount, "Extracted Invoice JSON", "invoice_header", "Invoice Amount"))
        {
            fields["InvoiceAmount"] = invoiceAmount!;
            fields["Invoice Amount"] = invoiceAmount!;
        }

        if (!HasValue(fields, "InvoiceDate", "Invoice Date", "DocumentDate"))
        {
            if (TryGetNestedString(root, out var invoiceDate, "Extracted Invoice JSON", "invoice_header", "Invoice Date")
                && !IsEmptyValue(invoiceDate))
            {
                fields["InvoiceDate"] = invoiceDate!;
                fields["Invoice Date"] = invoiceDate!;
            }
            else if (root.TryGetProperty("payment_terms", out var terms)
                && terms.ValueKind == JsonValueKind.Object
                && terms.TryGetProperty("invoice_date", out var invDateProp))
            {
                var invoiceDateFromTerms = JsonElementToString(invDateProp);
                if (!IsEmptyValue(invoiceDateFromTerms))
                    fields["InvoiceDate"] = invoiceDateFromTerms!;
            }
        }
    }

    private static bool HasValue(IReadOnlyDictionary<string, string> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value) && !IsEmptyValue(value))
                return true;
        }

        return false;
    }

    private static bool TryGetNestedString(
        JsonElement root,
        out string? value,
        params string[] path)
    {
        value = null;
        if (path.Length == 0)
            return false;

        var current = root;
        foreach (var segment in path)
        {
            if (!TryGetPropertyIgnoreCase(current, segment, out current))
                return false;
        }

        value = JsonElementToString(current);
        return !IsEmptyValue(value);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            value = prop.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetAgentResponseRoot(JsonElement element, out JsonElement root)
    {
        root = element;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var prop in element.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "AIAGENTResponse", StringComparison.OrdinalIgnoreCase))
                continue;

            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                root = prop.Value;
                return true;
            }

            break;
        }

        return true;
    }

    private static string? JsonElementToString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };

    /// <summary>AP invoice workflows: agent payloads often omit document type; default repository column to INVOICE.</summary>
    private static void EnsureApInvoiceDocumentType(Dictionary<string, string> fields)
    {
        if (fields.TryGetValue("DocumentType", out var existing) && !IsEmptyValue(existing))
            return;
        if (fields.TryGetValue("Document Type", out existing) && !IsEmptyValue(existing))
            return;

        fields.Remove("Document Type");
        fields["DocumentType"] = "INVOICE";
    }

    private static bool IsEmptyValue(string? value) =>
        string.IsNullOrWhiteSpace(value);

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

        if (EzfbColumnNaming.TryToColumnName(trimmed, out var fromJsonId) && ezfbColumns.Contains(fromJsonId))
        {
            column = fromJsonId;
            return true;
        }

        // Legacy: older code used F_ prefix for leading-digit jsonIds.
        if (EzfbColumnNaming.TryToColumnName(trimmed, out var baseName)
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
