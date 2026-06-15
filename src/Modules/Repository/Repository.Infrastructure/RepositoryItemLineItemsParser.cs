using System.Globalization;
using System.Text.Json;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

internal static class RepositoryItemLineItemsParser
{
    public static RepositoryItemLineItemsSectionDto? TryParse(string? summaryJson, string? ocrJson, string? currency)
    {
        if (!string.IsNullOrWhiteSpace(summaryJson) && TryParseJson(summaryJson, currency, out var fromSummary))
            return fromSummary;

        if (!string.IsNullOrWhiteSpace(ocrJson) && TryParseJson(ocrJson, currency, out var fromOcr))
            return fromOcr;

        return null;
    }

    private static bool TryParseJson(string json, string? currency, out RepositoryItemLineItemsSectionDto? section)
    {
        section = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryGetLineItemsArray(root, out var itemsElement))
                return false;

            var rows = new List<RepositoryItemLineItemRowDto>();
            foreach (var item in itemsElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                rows.Add(new RepositoryItemLineItemRowDto(
                    ReadString(item, "description", "Description", "itemDescription", "name"),
                    ReadDecimal(item, "qty", "quantity", "Qty", "Quantity"),
                    ReadDecimal(item, "unitPrice", "unit_price", "UnitPrice", "rate", "price"),
                    ReadDecimal(item, "gst", "tax", "GST", "gstAmount", "taxAmount"),
                    ReadDecimal(item, "total", "lineTotal", "amount", "Total")));
            }

            if (rows.Count == 0)
                return false;

            var grandTotal = ReadDecimal(root, "grandTotal", "grand_total", "GrandTotal", "totalAmount", "invoiceTotal");
            if (grandTotal == null)
                grandTotal = rows.Sum(r => r.Total ?? 0);

            section = new RepositoryItemLineItemsSectionDto(rows, grandTotal, currency);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetLineItemsArray(JsonElement root, out JsonElement array)
    {
        foreach (var name in new[] { "lineItems", "line_items", "LineItems", "items", "invoiceLines" })
        {
            if (root.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array)
                return true;
        }

        array = default;
        return false;
    }

    private static string? ReadString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var prop))
                continue;

            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var prop))
                continue;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var num))
                return num;

            if (prop.ValueKind == JsonValueKind.String &&
                decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return null;
    }
}
