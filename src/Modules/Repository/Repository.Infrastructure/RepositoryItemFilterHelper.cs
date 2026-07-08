using System.Text.Json;
using Microsoft.Data.SqlClient;
using SaaSApp.Repository.Application;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

internal static class RepositoryItemFilterHelper
{
    private static readonly string[] BuiltInOperationalColumns =
    [
        "FileName", "Status", "OcrScore", "AiStatus", "CreatedAtUtc", "ModifiedAtUtc"
    ];

    public static ItemListFilterSchemaDto BuildFilterSchema(RepositoryDetailDto repo)
    {
        var fields = new List<ItemListFilterFieldDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in repo.Fields.OrderBy(f => f.Level).ThenBy(f => f.Name))
        {
            var sqlColumn = RepositorySqlHelper.SanitizeColumnName(field.SqlColumnName);
            if (!seen.Add(sqlColumn))
                continue;

            fields.Add(new ItemListFilterFieldDto(field.Name, sqlColumn, field.DataType));
        }

        return new ItemListFilterSchemaDto(fields);
    }

    public static HashSet<string> BuildFilterableColumns(
        RepositoryDetailDto repo,
        IReadOnlySet<string>? tableColumns = null)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in BuildFilterSchema(repo).Fields)
            set.Add(f.SqlColumnName);

        foreach (var col in BuiltInOperationalColumns)
        {
            if (tableColumns is null || tableColumns.Contains(col))
                set.Add(col);
        }

        return set;
    }

    public static IReadOnlyDictionary<string, string> ParseFilters(string? json) =>
        RepositoryMetadataParser.Parse(json);

    public static IReadOnlyDictionary<string, string> ParseMetadataJson(string? json) =>
        RepositoryMetadataParser.Parse(json);

    public static bool TryResolveFilterColumn(
        string key,
        HashSet<string> allowedColumns,
        RepositoryDetailDto repo,
        out string column)
    {
        column = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (allowedColumns.Contains(key))
        {
            var matchedField = repo.Fields.FirstOrDefault(f =>
                string.Equals(f.SqlColumnName, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Name, key, StringComparison.OrdinalIgnoreCase));

            column = RepositorySqlHelper.SanitizeColumnName(matchedField?.SqlColumnName ?? key);
            return true;
        }

        var field = repo.Fields.FirstOrDefault(f =>
            string.Equals(f.Name, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.SqlColumnName, key, StringComparison.OrdinalIgnoreCase));

        if (field != null && allowedColumns.Contains(field.SqlColumnName))
        {
            column = RepositorySqlHelper.SanitizeColumnName(field.SqlColumnName);
            return true;
        }

        return false;
    }

    public static string ResolveFilterColumn(string key, HashSet<string> allowedColumns, RepositoryDetailDto repo)
    {
        if (TryResolveFilterColumn(key, allowedColumns, repo, out var column))
            return column;

        throw new ArgumentException(
            $"Unknown filter field '{key}'. Use GET .../items/filter-fields for allowed keys.");
    }

    public static void ApplyEqualityFilters(
        ICollection<string> where,
        IList<SqlParameter> parameters,
        IReadOnlyDictionary<string, string> filters,
        HashSet<string> allowedColumns,
        RepositoryDetailDto repo,
        string tableAlias = "i")
    {
        var index = 0;
        foreach (var (key, value) in filters)
        {
            var col = ResolveFilterColumn(key, allowedColumns, repo);
            var param = $"@F{index++}";
            var prefix = string.IsNullOrEmpty(tableAlias) ? string.Empty : $"{tableAlias}.";
            where.Add($"{prefix}[{col}] = {param}");
            parameters.Add(new Microsoft.Data.SqlClient.SqlParameter(param, value));
        }
    }

    public static string ResolveSortColumn(string sortBy, HashSet<string> allowedColumns, HashSet<string>? tableColumns = null)
    {
        var mapped = RepositorySqlHelper.MapSortColumn(sortBy);
        var candidates = SortColumnCandidates(mapped, sortBy);
        foreach (var col in candidates)
        {
            if (!allowedColumns.Contains(col))
                continue;
            if (tableColumns != null && !tableColumns.Contains(col))
                continue;

            return col;
        }

        return allowedColumns.Contains("CreatedAtUtc") ? "CreatedAtUtc" : "FileName";
    }

    private static IEnumerable<string> SortColumnCandidates(string mapped, string rawSortBy)
    {
        yield return mapped;

        if (!string.Equals(mapped, rawSortBy, StringComparison.OrdinalIgnoreCase))
            yield return RepositorySqlHelper.SanitizeColumnName(rawSortBy);

        foreach (var col in mapped switch
        {
            "DocumentDate" => new[] { "InvoiceDate", "PODate" },
            "Supplier" => new[] { "VendorName", "Vendor" },
            "InvoiceNumber" => new[] { "InvoiceNo" },
            "Amount" => new[] { "InvoiceAmount", "POAmount" },
            "AiStatus" => new[] { "MatchedStatus" },
            "Status" => new[] { "StageStatus" },
            "OcrScore" => Array.Empty<string>(),
            _ => Array.Empty<string>()
        })
        {
            yield return col;
        }
    }
}
