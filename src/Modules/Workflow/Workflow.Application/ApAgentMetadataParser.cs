using System.Text.Json;

namespace SaaSApp.Workflow.Application;

/// <summary>
/// Parses AP agent metadata bodies with nested invoice_header and Line Item sections.
/// </summary>
public static class ApAgentMetadataParser
{
    private static readonly string[] InvoiceHeaderKeys = ["invoice_header", "invoiceHeader", "header"];
    /// <summary>Accepted JSON property names for the line-items array (case-insensitive).</summary>
    public static readonly string[] LineItemKeys =
    [
        "Line Item",
        "Line Items",
        "line item",
        "line items",
        "lineItems",
        "lineItem",
        "line_items",
        "line_item",
        "LineItem",
        "LineItems",
        "invoice_line_items",
        "invoice extracted line item",
        "invoice_extracted_line_item",
        "Invoice Extracted Line Item",
        "invoiceExtractedLineItems"
    ];

    private static readonly Dictionary<string, string> HeaderToRepositoryAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Invoice No"] = "InvoiceNumber",
            ["InvoiceNo"] = "InvoiceNumber",
            ["Vendor Name"] = "Supplier",
            ["VendorName"] = "Supplier",
            ["Invoice Date"] = "DocumentDate",
            ["InvoiceDate"] = "DocumentDate",
            ["Invoice Amount"] = "Amount",
            ["InvoiceAmount"] = "Amount",
            ["Invoice Tax Amount"] = "InvoiceTaxAmount",
            ["Subtotal"] = "Subtotal",
            ["currency"] = "Currency",
            ["TERMS"] = "Terms",
            ["Terms"] = "Terms",
            ["Matter ID"] = "MatterId",
            ["GL Account"] = "GlAccount",
            ["GL Category"] = "GlCategory",
            ["Department"] = "Department",
            ["Cost Center"] = "CostCenter",
            ["Matched Status"] = "MatchedStatus",
            ["PoNumber"] = "PoNumber",
            ["PONumber"] = "PoNumber",
            ["PO Number"] = "PoNumber",
        };

    public static (IReadOnlyDictionary<string, string> Fields, string? LineItemsJson) ParseFieldsPayload(JsonElement fieldsRoot)
    {
        if (fieldsRoot.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("fields must be a JSON object.");

        var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? lineItemsJson = null;

        foreach (var headerKey in InvoiceHeaderKeys)
        {
            if (!TryGetPropertyIgnoreCase(fieldsRoot, headerKey, out var header) || header.ValueKind != JsonValueKind.Object)
                continue;

            FlattenObject(header, flat);
            break;
        }

        if (TryGetLineItemsElement(fieldsRoot, out var linesElement))
        {
            lineItemsJson = ElementToLineItemsJson(linesElement)
                ?? throw new ArgumentException("Line items must be a JSON array (or JSON string containing an array).");
        }

        if (flat.Count == 0)
        {
            foreach (var prop in fieldsRoot.EnumerateObject())
            {
                if (IsReservedSectionKey(prop.Name))
                    continue;

                if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    continue;

                var value = JsonValueToString(prop.Value);
                if (!string.IsNullOrWhiteSpace(value))
                    flat[prop.Name.Trim()] = value;
            }
        }

        AddRepositoryAliases(flat);
        return (flat, lineItemsJson);
    }

    private static void FlattenObject(JsonElement obj, Dictionary<string, string> target)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                continue;

            var value = JsonValueToString(prop.Value);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            target[prop.Name.Trim()] = value;
        }
    }

    private static void AddRepositoryAliases(Dictionary<string, string> flat)
    {
        foreach (var (key, value) in flat.ToList())
        {
            if (!HeaderToRepositoryAliases.TryGetValue(key, out var alias))
                continue;
            if (!flat.ContainsKey(alias))
                flat[alias] = value;
        }
    }

    private static bool IsReservedSectionKey(string name) =>
        InvoiceHeaderKeys.Any(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase))
        || IsLineItemSectionName(name);

    /// <summary>True when property name is the line-items block (e.g. wFormControl name "invoice extracted line item").</summary>
    public static bool IsLineItemSectionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (LineItemKeys.Any(k => string.Equals(k, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return true;

        var normalized = NormalizeName(name);
        if (normalized.Contains("polineitem", StringComparison.Ordinal)
            && !normalized.Contains("invoiceextracted", StringComparison.Ordinal))
            return false;

        if (normalized is "lineitem" or "lineitems")
            return true;

        if (normalized.Contains("lineitems", StringComparison.Ordinal)
            && !normalized.Contains("invoiceheader", StringComparison.Ordinal)
            && !normalized.Contains("poline", StringComparison.Ordinal)
            && !normalized.StartsWith("po", StringComparison.Ordinal))
            return true;

        return normalized.Contains("invoiceextracted", StringComparison.Ordinal)
            && normalized.Contains("lineitem", StringComparison.Ordinal);
    }

    /// <summary>Line items at request root (outside <c>fields</c>), any accepted alias.</summary>
    public static string? TryGetRootLineItemsJson(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
            return null;

        if (TryGetLineItemsElement(body, out var element))
            return ElementToLineItemsJson(element);

        return null;
    }

    private static string? ElementToLineItemsJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => element.GetRawText(),
        JsonValueKind.String => element.GetString(),
        _ => null
    };

    private static bool TryGetLineItemsElement(JsonElement fieldsRoot, out JsonElement linesElement)
    {
        linesElement = default;

        foreach (var lineKey in LineItemKeys)
        {
            if (!TryGetPropertyIgnoreCase(fieldsRoot, lineKey, out var candidate))
                continue;

            if (candidate.ValueKind is JsonValueKind.Array or JsonValueKind.String)
            {
                linesElement = candidate;
                return true;
            }
        }

        foreach (var prop in fieldsRoot.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array)
                continue;
            if (!IsLineItemSectionName(prop.Name))
                continue;

            linesElement = prop.Value;
            return true;
        }

        return false;
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

    private static string NormalizeName(string name) =>
        name.Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToLowerInvariant();

    private static string? JsonValueToString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => null,
        _ => null
    };
}
