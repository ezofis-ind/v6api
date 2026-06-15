using System.Globalization;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

internal static class RepositoryItemWorkspaceBuilder
{
    private static readonly HashSet<string> HiddenColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code", "ValidFrom", "ValidTo", "TenantId", "RepositoryId", "FolderId", "StorageProviderId",
        "FilePath", "FileName", "FileType", "FileSize", "TotalPages", "IsDeleted", "ModifiedAtUtc", "ModifiedBy",
        "WorkflowInstanceId", "OcrText", "OcrJson", "SummaryJson", "ActiveItem", "IsVerified",
        "EncryptPassword", "EncryptStatus", "EncryptedBy", "ActivityBy", "ActivityOn", "ActivityId",
        "ArchivedFrom", "ArchivedAt", "FileVersion", "Revision", "StageStatus", "MailId"
    };

    private static readonly (string Column, string Label)[] DocumentInfoSpecs =
    [
        ("InvoiceNumber", "Invoice Number"),
        ("DocumentType", "Document Type"),
        ("DocumentDate", "Invoice Date"),
        ("DueDate", "Due Date"),
        ("Amount", "Amount"),
        ("Status", "Status"),
        ("ApprovalStage", "Approval Stage"),
    ];

    private static readonly (string Column, string Label, Func<object?, string?> Format)[] AiAnalysisSpecs =
    [
        ("OcrScore", "OCR Confidence", FormatOcrPercent),
        ("AiStatus", "AI Validation", FormatPlain),
        ("MatchedStatus", "Duplicate Check", FormatPlain),
        ("RiskLevel", "Risk Level", FormatPlain),
    ];

    private static readonly (string Column, string Label)[] SystemInfoSpecs =
    [
        ("CreatedBy", "Uploaded By"),
        ("CreatedAtUtc", "Upload Date"),
        ("Department", "Department"),
        ("Source", "Source Channel"),
        ("Id", "Document ID"),
    ];

    private static readonly HashSet<string> DocumentInfoColumns =
        DocumentInfoSpecs.Select(s => s.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AiAnalysisColumns =
        AiAnalysisSpecs.Select(s => s.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SystemInfoColumns =
        SystemInfoSpecs.Select(s => s.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> SupplierLabelOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Supplier"] = "Supplier Name",
            ["VendorName"] = "Vendor Name",
            ["PoNumber"] = "PO Reference",
            ["Terms"] = "Payment Terms",
            ["GSTIN"] = "GSTIN",
            ["PAN"] = "PAN",
            ["IFSCCode"] = "IFSC Code",
            ["VendorAddress"] = "Vendor Address",
            ["Buyer"] = "Buyer",
            ["PayToAddress"] = "Pay To Address",
            ["ShipToAddress"] = "Ship To Address",
            ["PODate"] = "PO Date",
            ["POAmount"] = "PO Amount",
        };

    public static RepositoryItemWorkspaceDto Build(
        Guid repositoryId,
        RepositoryDetailDto repository,
        Guid itemId,
        string? fileName,
        string? fileType,
        int? fileSize,
        Guid storageProviderId,
        string? storageProviderCode,
        IReadOnlyDictionary<string, object?> fields)
    {
        var currency = GetString(fields, "Currency");
        var fileUrl = $"/api/repositories/{repositoryId:D}/items/{itemId:D}/file";

        var documentInfo = BuildDocumentInfo(fields, currency);
        var supplierDetails = BuildSupplierDetails(repository.Fields, fields, currency);
        var aiAnalysis = BuildAiAnalysis(fields);
        var systemInfo = BuildSystemInfo(itemId, fields);
        var lineItems = RepositoryItemLineItemsParser.TryParse(
            GetString(fields, "SummaryJson"),
            GetString(fields, "OcrJson"),
            currency);

        var detailsRow = new[]
        {
            documentInfo,
            supplierDetails,
            aiAnalysis,
            systemInfo
        };

        return new RepositoryItemWorkspaceDto(
            itemId,
            fileName,
            fileType,
            fileSize,
            fileUrl,
            storageProviderId,
            storageProviderCode,
            detailsRow,
            lineItems);
    }

    private static RepositoryItemPanelSectionDto BuildDocumentInfo(
        IReadOnlyDictionary<string, object?> fields,
        string? currency)
    {
        var panelFields = new List<RepositoryItemPanelFieldDto>();

        foreach (var (column, label) in DocumentInfoSpecs)
        {
            if (!fields.TryGetValue(column, out var raw) || raw == null)
            {
                if (column == "ApprovalStage")
                    raw = ResolveApprovalStage(fields);
                else
                    continue;
            }

            var value = column switch
            {
                "Amount" => FormatMoney(raw, currency),
                "DocumentDate" or "DueDate" => FormatDate(raw),
                _ => FormatPlain(raw)
            };

            if (value != null)
                panelFields.Add(new RepositoryItemPanelFieldDto(column, label, value));
        }

        return new RepositoryItemPanelSectionDto("documentInfo", "Document Info", panelFields);
    }

    private static RepositoryItemPanelSectionDto BuildSupplierDetails(
        IReadOnlyList<RepositoryFieldDto> repositoryFields,
        IReadOnlyDictionary<string, object?> fields,
        string? currency)
    {
        var panelFields = new List<RepositoryItemPanelFieldDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in repositoryFields.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            var column = field.SqlColumnName;
            if (IsExcludedFromSupplier(column) || !seen.Add(column))
                continue;

            if (!fields.TryGetValue(column, out var raw) || raw == null)
                continue;

            var label = !string.IsNullOrWhiteSpace(field.Name)
                ? field.Name
                : SupplierLabelOverrides.GetValueOrDefault(column, column);

            var value = IsMoneyColumn(column)
                ? FormatMoney(raw, currency)
                : IsDateColumn(column)
                    ? FormatDate(raw)
                    : FormatPlain(raw);

            if (value != null)
                panelFields.Add(new RepositoryItemPanelFieldDto(column, label, value));
        }

        AppendSupplierFallback(fields, panelFields, seen, currency);

        return new RepositoryItemPanelSectionDto("supplierDetails", "Supplier Details", panelFields);
    }

    private static void AppendSupplierFallback(
        IReadOnlyDictionary<string, object?> fields,
        List<RepositoryItemPanelFieldDto> panelFields,
        HashSet<string> seen,
        string? currency)
    {
        foreach (var column in SupplierLabelOverrides.Keys)
        {
            if (seen.Contains(column) || IsExcludedFromSupplier(column))
                continue;

            if (!fields.TryGetValue(column, out var raw) || raw == null)
                continue;

            var value = IsMoneyColumn(column) ? FormatMoney(raw, currency) : FormatPlain(raw);
            if (value != null)
                panelFields.Add(new RepositoryItemPanelFieldDto(column, SupplierLabelOverrides[column], value));
        }
    }

    private static bool IsExcludedFromSupplier(string column) =>
        HiddenColumns.Contains(column) ||
        DocumentInfoColumns.Contains(column) ||
        AiAnalysisColumns.Contains(column) ||
        SystemInfoColumns.Contains(column);

    private static RepositoryItemPanelSectionDto BuildAiAnalysis(IReadOnlyDictionary<string, object?> fields)
    {
        var panelFields = new List<RepositoryItemPanelFieldDto>();

        foreach (var (column, label, format) in AiAnalysisSpecs)
        {
            if (!fields.TryGetValue(column, out var raw) || raw == null)
                continue;

            var value = format(raw);
            if (value != null)
                panelFields.Add(new RepositoryItemPanelFieldDto(column, label, value));
        }

        return new RepositoryItemPanelSectionDto("aiAnalysis", "AI Analysis", panelFields);
    }

    private static RepositoryItemPanelSectionDto BuildSystemInfo(Guid itemId, IReadOnlyDictionary<string, object?> fields)
    {
        var panelFields = new List<RepositoryItemPanelFieldDto>();

        foreach (var (column, label) in SystemInfoSpecs)
        {
            string? value;
            if (column == "Id")
                value = FormatDocumentId(itemId);
            else if (column == "CreatedBy")
                value = FormatUploadedBy(fields);
            else if (column == "CreatedAtUtc")
            {
                if (!fields.TryGetValue(column, out var raw) || raw == null)
                    continue;
                value = FormatDate(raw);
            }
            else
            {
                if (!fields.TryGetValue(column, out var raw) || raw == null)
                    continue;
                value = FormatPlain(raw);
            }

            if (value != null)
                panelFields.Add(new RepositoryItemPanelFieldDto(column, label, value));
        }

        return new RepositoryItemPanelSectionDto("systemInfo", "System Info", panelFields);
    }

    private static object? ResolveApprovalStage(IReadOnlyDictionary<string, object?> fields)
    {
        if (fields.TryGetValue("ApprovalStage", out var stage) && stage != null)
            return stage;

        if (fields.TryGetValue("StageStatus", out var stageStatus) && stageStatus != null)
            return stageStatus;

        return null;
    }

    private static string? FormatUploadedBy(IReadOnlyDictionary<string, object?> fields)
    {
        if (fields.TryGetValue("CreatedBy", out var createdBy) && createdBy != null)
        {
            if (createdBy is Guid guid)
                return guid.ToString("D");

            var text = createdBy.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        var source = GetString(fields, "Source");
        if (!string.IsNullOrWhiteSpace(source))
            return $"System ({source})";

        return "System";
    }

    private static string FormatDocumentId(Guid itemId) =>
        $"DOC-{itemId.ToString("N")[..6].ToUpperInvariant()}";

    private static string? FormatOcrPercent(object? raw)
    {
        if (raw == null)
            return null;

        if (raw is byte b)
            return $"{b}%";

        if (raw is short or int or long)
            return $"{Convert.ToInt64(raw)}%";

        if (decimal.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var score))
            return score <= 1 ? $"{score * 100:0}%" : $"{score:0}%";

        return FormatPlain(raw);
    }

    private static string? FormatMoney(object? raw, string? currency)
    {
        if (raw == null)
            return null;

        if (!decimal.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return FormatPlain(raw);

        var symbol = CurrencySymbol(currency);
        return string.IsNullOrEmpty(symbol)
            ? amount.ToString("N2", CultureInfo.InvariantCulture)
            : $"{symbol}{amount:N0}";
    }

    private static string CurrencySymbol(string? currency) => currency?.Trim().ToUpperInvariant() switch
    {
        "INR" or "RS" or "₹" => "₹",
        "USD" or "$" => "$",
        "EUR" or "€" => "€",
        "GBP" => "£",
        _ => string.IsNullOrWhiteSpace(currency) ? "" : currency + " "
    };

    private static string? FormatDate(object? raw)
    {
        if (raw == null)
            return null;

        if (raw is DateTime dt)
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (raw is DateOnly d)
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (DateTime.TryParse(raw.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return FormatPlain(raw);
    }

    private static string? FormatPlain(object? raw) =>
        raw switch
        {
            null => null,
            DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool b => b ? "Yes" : "No",
            _ => raw.ToString()
        };

    private static string? GetString(IReadOnlyDictionary<string, object?> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static bool IsMoneyColumn(string column) =>
        column.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(column, "Amount", StringComparison.OrdinalIgnoreCase);

    private static bool IsDateColumn(string column) =>
        column.Contains("Date", StringComparison.OrdinalIgnoreCase);
}
