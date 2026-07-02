using System.Text.Json;
using System.Text.RegularExpressions;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure.Services;

internal static class OcrFieldParameterBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Regex ParameterSegmentRegex = new(
        @"(?<name>[^,]+?)\s*,\s*(?<type>SHORT_TEXT|DATE|NUMBER|LONG_TEXT|TABLE|DECIMAL|NUMERIC|TEXT|STRING|LONGTEXT)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<string> BuildParameters(string? fieldsJson, RepositoryDetailDto? repo)
    {
        if (!string.IsNullOrWhiteSpace(fieldsJson))
        {
            var fromInput = ParseFieldsInput(fieldsJson);
            if (fromInput.Count > 0)
                return fromInput;
        }

        if (repo == null)
            return Array.Empty<string>();

        return repo.Fields
            .Where(f => !IsTableDataType(f.DataType))
            .OrderBy(f => f.Level)
            .ThenBy(f => f.OrderId ?? int.MaxValue)
            .Select(f => FormatParameterLine(f.Name, MapDataType(f.DataType)))
            .ToList();
    }

    public static IReadOnlyList<Dictionary<string, IReadOnlyList<string>>> BuildTableParameters(RepositoryDetailDto? repo)
    {
        if (repo == null)
            return Array.Empty<Dictionary<string, IReadOnlyList<string>>>();

        return repo.Fields
            .Where(f => IsTableDataType(f.DataType))
            .Select(f => new Dictionary<string, IReadOnlyList<string>>
            {
                [f.Name] = Array.Empty<string>()
            })
            .ToList();
    }

    private static List<string> ParseFieldsInput(string fieldsJson)
    {
        var trimmed = NormalizeFieldsJsonInput(fieldsJson);
        if (string.IsNullOrWhiteSpace(trimmed))
            return new List<string>();

        if (!trimmed.StartsWith('['))
        {
            var segments = SplitMultiParameterText(trimmed);
            if (segments.Count > 0)
                return segments;

            if (LooksLikeParameterLine(trimmed))
                return new List<string> { NormalizeParameterLine(trimmed) };

            throw new ArgumentException(
                "fields must be a JSON array, e.g. [\"Invoice Number,SHORT_TEXT\",\"Invoice Date,DATE\"] " +
                "or [{\"name\":\"Invoice Number\",\"type\":\"SHORT_TEXT\"}].");
        }

        if (trimmed.Contains("SHORT_TEXT", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(",DATE", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(", DATE", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(",NUMBER", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(", NUMBER", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(",LONG_TEXT", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(", LONG_TEXT", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(",TABLE", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(", TABLE", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var lines = JsonSerializer.Deserialize<List<string>>(trimmed, JsonOptions);
                if (lines is { Count: > 0 })
                    return ExpandParameterLines(lines);
            }
            catch (JsonException)
            {
                // fall through to object array parse
            }
        }

        try
        {
            var fields = JsonSerializer.Deserialize<List<UploadIndexFieldDto>>(trimmed, JsonOptions);
            if (fields == null || fields.Count == 0)
                return new List<string>();

            return fields
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .Select(f => NormalizeParameterLine($"{f.Name.Trim()},{MapDataType(f.Type)}"))
                .ToList();
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                "fields must be a JSON array, e.g. [\"Invoice Number,SHORT_TEXT\",\"Invoice Date,DATE\"] " +
                "or [{\"name\":\"Invoice Number\",\"type\":\"SHORT_TEXT\"}].",
                ex);
        }
    }

    private static string NormalizeFieldsJsonInput(string fieldsJson)
    {
        var trimmed = fieldsJson.Trim();
        if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
            trimmed = trimmed[1..].Trim();

        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            try
            {
                var unwrapped = JsonSerializer.Deserialize<string>(trimmed, JsonOptions);
                if (!string.IsNullOrWhiteSpace(unwrapped))
                    trimmed = unwrapped.Trim();
            }
            catch (JsonException)
            {
                // keep original
            }
        }

        if (trimmed.StartsWith('{') && !trimmed.StartsWith("[{"))
            return $"[{trimmed}]";

        if (trimmed.StartsWith('['))
            return trimmed;

        var segments = SplitMultiParameterText(trimmed);
        if (segments.Count > 0)
            return JsonSerializer.Serialize(segments, JsonOptions);

        return trimmed;
    }

    private static List<string> ExpandParameterLines(IEnumerable<string> lines)
    {
        var result = new List<string>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var trimmed = line.Trim();
            var segments = SplitMultiParameterText(trimmed);
            if (segments.Count > 0)
            {
                result.AddRange(segments);
                continue;
            }

            if (LooksLikeParameterLine(trimmed))
                result.Add(NormalizeParameterLine(trimmed));
        }

        return result;
    }

    private static List<string> SplitMultiParameterText(string value)
    {
        var matches = ParameterSegmentRegex.Matches(value);
        if (matches.Count == 0)
            return new List<string>();

        return matches
            .Select(m => FormatParameterLine(m.Groups["name"].Value.Trim(), m.Groups["type"].Value))
            .ToList();
    }

    private static bool LooksLikeParameterLine(string value)
    {
        var comma = value.IndexOf(',');
        if (comma <= 0)
            return false;

        var type = value[(comma + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(type);
    }

    private static string NormalizeParameterLine(string line)
    {
        var comma = line.IndexOf(',');
        if (comma < 0)
            return line.Trim();

        var name = line[..comma].Trim();
        var type = line[(comma + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(name))
            return line.Trim();

        return FormatParameterLine(name, MapDataType(type));
    }

    private static string FormatParameterLine(string name, string type) =>
        $"{name.Trim()}, {MapDataType(type)}";

    private static string MapDataType(string? dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return "SHORT_TEXT";

        var upper = dataType.Trim().ToUpperInvariant();
        return upper switch
        {
            "DATE" => "DATE",
            "NUMBER" or "DECIMAL" or "NUMERIC" or "INT" or "INTEGER" => "NUMBER",
            "LONG_TEXT" or "LONGTEXT" => "LONG_TEXT",
            "TABLE" => "TABLE",
            "SHORT_TEXT" or "TEXT" or "STRING" => "SHORT_TEXT",
            _ when upper.Contains("DATE", StringComparison.Ordinal) => "DATE",
            _ when upper.Contains("NUM", StringComparison.Ordinal) => "NUMBER",
            _ => "SHORT_TEXT"
        };
    }

    private static bool IsTableDataType(string? dataType) =>
        string.Equals(dataType, "TABLE", StringComparison.OrdinalIgnoreCase);
}
