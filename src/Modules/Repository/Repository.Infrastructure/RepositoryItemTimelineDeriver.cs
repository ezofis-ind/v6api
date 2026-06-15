using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

/// <summary>Read-only timeline entries inferred from item columns when no stored events exist.</summary>
internal static class RepositoryItemTimelineDeriver
{
    public static IReadOnlyList<RepositoryItemTimelineEventDto> Derive(IReadOnlyDictionary<string, object?> fields)
    {
        var events = new List<RepositoryItemTimelineEventDto>();

        if (TryGetDate(fields, "CreatedAtUtc", out var createdAt))
        {
            var source = GetString(fields, "Source");
            var channel = string.IsNullOrWhiteSpace(source) ? "upload" : source.Trim();
            events.Add(new RepositoryItemTimelineEventDto(
                Guid.Empty,
                "system",
                $"Document ingested via {channel.ToLowerInvariant()}",
                null,
                "System",
                "System",
                createdAt,
                IsDerived: true));
        }

        if (TryGetByte(fields, "OcrScore", out var ocrScore) && TryGetDate(fields, "CreatedAtUtc", out var ocrAt))
        {
            events.Add(new RepositoryItemTimelineEventDto(
                Guid.Empty,
                "ai",
                $"OCR extraction complete — {ocrScore}% confidence",
                null,
                "AI Engine",
                "AI Engine",
                ocrAt.AddMinutes(1),
                IsDerived: true));
        }

        if (HasAiValidation(fields) && TryGetDate(fields, "CreatedAtUtc", out var aiAt))
        {
            var matched = GetString(fields, "MatchedStatus");
            var detail = string.IsNullOrWhiteSpace(matched) || matched.Equals("Clean", StringComparison.OrdinalIgnoreCase)
                ? "Metadata validated, no duplicates found"
                : $"Metadata validated, duplicates: {matched}";

            events.Add(new RepositoryItemTimelineEventDto(
                Guid.Empty,
                "ai",
                detail,
                null,
                "AI Engine",
                "AI Engine",
                aiAt.AddMinutes(2),
                IsDerived: true));
        }

        return events.OrderBy(e => e.CreatedAtUtc).ToList();
    }

    private static bool HasAiValidation(IReadOnlyDictionary<string, object?> fields) =>
        fields.ContainsKey("AiStatus") && fields["AiStatus"] != null ||
        fields.ContainsKey("MatchedStatus") && fields["MatchedStatus"] != null;

    private static string? GetString(IReadOnlyDictionary<string, object?> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static bool TryGetDate(IReadOnlyDictionary<string, object?> fields, string key, out DateTime value)
    {
        value = default;
        if (!fields.TryGetValue(key, out var raw) || raw == null)
            return false;

        if (raw is DateTime dt)
        {
            value = dt;
            return true;
        }

        return DateTime.TryParse(raw.ToString(), out value);
    }

    private static bool TryGetByte(IReadOnlyDictionary<string, object?> fields, string key, out byte value)
    {
        value = default;
        if (!fields.TryGetValue(key, out var raw) || raw == null)
            return false;

        if (raw is byte b)
        {
            value = b;
            return true;
        }

        return byte.TryParse(raw.ToString(), out value);
    }
}
