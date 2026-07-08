using System.Globalization;
using System.Text.Json;

namespace SaaSApp.Repository.Application;

/// <summary>Extracts list/detail metadata from repository OcrJson / SummaryJson payloads.</summary>
public static class RepositoryOcrJsonMetadataExtractor
{
    private static readonly string[] HeaderSectionKeys =
        ["invoice_header", "invoiceHeader", "header", "Extracted Invoice JSON", "po_row"];

    private static readonly Dictionary<string, string> CanonicalAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Invoice No"] = "InvoiceNumber",
            ["InvoiceNo"] = "InvoiceNumber",
            ["Vendor Name"] = "Supplier",
            ["VendorName"] = "Supplier",
            ["Vendor"] = "Supplier",
            ["Invoice Date"] = "DocumentDate",
            ["InvoiceDate"] = "DocumentDate",
            ["PO DATE"] = "DocumentDate",
            ["PO Date"] = "DocumentDate",
            ["Invoice Amount"] = "Amount",
            ["InvoiceAmount"] = "Amount",
            ["PO Amount"] = "Amount",
            ["POAmount"] = "Amount",
            ["currency"] = "Currency",
            ["TERMS"] = "Terms",
            ["Terms"] = "Terms",
            ["Matched Status"] = "AiStatus",
            ["MatchedStatus"] = "AiStatus",
            ["Document Type"] = "DocumentType",
            ["PO Number"] = "PoNumber",
            ["PONumber"] = "PoNumber",
            ["OCR Confidence"] = "OcrScore",
            ["OcrPercent"] = "OcrScore",
        };

    public static IReadOnlyDictionary<string, string> Extract(string? ocrJson, string? summaryJson)
    {
        var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        TryMergeJson(flat, ocrJson);
        TryMergeJson(flat, summaryJson);

        foreach (var (key, value) in flat.ToList())
        {
            if (!CanonicalAliases.TryGetValue(key, out var canonical))
                continue;
            if (!flat.ContainsKey(canonical))
                flat[canonical] = value;
        }

        return flat;
    }

    private static void TryMergeJson(Dictionary<string, string> target, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            FlattenElement(doc.RootElement, target);
        }
        catch (JsonException)
        {
            // Ignore invalid stored JSON.
        }
    }

    private static void FlattenElement(JsonElement element, Dictionary<string, string> target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var headerKey in HeaderSectionKeys)
                {
                    if (!TryGetPropertyIgnoreCase(element, headerKey, out var section)
                        || section.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (string.Equals(headerKey, "Extracted Invoice JSON", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(headerKey, "po_row", StringComparison.OrdinalIgnoreCase))
                    {
                        FlattenElement(section, target);
                        FlattenScalars(section, target);
                        continue;
                    }

                    FlattenScalars(section, target);
                    break;
                }

                FlattenScalars(element, target);
                break;
            case JsonValueKind.String:
                var text = element.GetString();
                if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith('{'))
                    TryMergeJson(target, text);
                break;
        }
    }

    private static void FlattenScalars(JsonElement obj, Dictionary<string, string> target)
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

    private static string? JsonValueToString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };

    public static string? TryInferInvoiceNumberFromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var stem = Path.GetFileNameWithoutExtension(fileName.Trim());
        if (string.IsNullOrWhiteSpace(stem))
            return null;

        var versionSuffix = stem.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
        if (versionSuffix > 0 && int.TryParse(stem[(versionSuffix + 2)..], out _))
            stem = stem[..versionSuffix];

        return stem.Contains("INV", StringComparison.OrdinalIgnoreCase) ? stem : null;
    }

    public static byte? TryParseOcrScore(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
            return score;

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
            return dec <= 1 ? (byte)Math.Clamp(dec * 100, 0, 100) : (byte)Math.Clamp(dec, 0, 255);

        return null;
    }
}
