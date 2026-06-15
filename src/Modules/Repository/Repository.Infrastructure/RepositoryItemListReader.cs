using Microsoft.Data.SqlClient;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

internal static class RepositoryItemListReader
{
    private static readonly string[] OptionalListColumns =
    [
        "DocumentType", "Supplier", "InvoiceNumber", "PoNumber", "DocumentDate",
        "Amount", "Currency", "RiskLevel", "Source", "Department", "Status", "OcrScore", "AiStatus",
        "FileVersion"
    ];

    public static string BuildSelectList(HashSet<string> tableColumns)
    {
        var parts = new List<string> { "i.Id", "i.FileName" };
        foreach (var col in OptionalListColumns)
        {
            if (RepositoryItemTableColumns.Has(tableColumns, col))
                parts.Add($"i.[{col}]");
        }

        parts.Add("i.StorageProviderId");
        parts.Add("sp.Code");
        if (RepositoryItemTableColumns.Has(tableColumns, "FilePath"))
            parts.Add("i.FilePath");

        return string.Join(", ", parts);
    }

    public static RepositoryItemListDto ReadRow(SqlDataReader reader, HashSet<string> tableColumns)
    {
        var id = reader.GetGuid(reader.GetOrdinal("Id"));
        var fileName = GetString(reader, "FileName");

        return new RepositoryItemListDto(
            id,
            fileName,
            GetInt32(reader, tableColumns, "FileVersion"),
            GetString(reader, tableColumns, "DocumentType"),
            GetString(reader, tableColumns, "Supplier"),
            GetString(reader, tableColumns, "InvoiceNumber"),
            GetString(reader, tableColumns, "PoNumber"),
            GetDateTime(reader, tableColumns, "DocumentDate"),
            GetDecimal(reader, tableColumns, "Amount"),
            GetString(reader, tableColumns, "Currency"),
            GetString(reader, tableColumns, "Status"),
            GetByte(reader, tableColumns, "OcrScore"),
            GetString(reader, tableColumns, "AiStatus"),
            GetString(reader, tableColumns, "RiskLevel"),
            GetString(reader, tableColumns, "Source"),
            GetString(reader, tableColumns, "Department"),
            reader.GetGuid(reader.GetOrdinal("StorageProviderId")),
            reader.GetString(reader.GetOrdinal("Code")),
            RepositoryItemTableColumns.Has(tableColumns, "FilePath")
                && !reader.IsDBNull(reader.GetOrdinal("FilePath"))
                && !string.IsNullOrWhiteSpace(reader.GetString(reader.GetOrdinal("FilePath"))));
    }

    public static string? ResolveDateFilterColumn(HashSet<string> tableColumns, RepositoryDetailDto repo)
    {
        if (RepositoryItemTableColumns.Has(tableColumns, "DocumentDate"))
            return "DocumentDate";

        var dateField = repo.Fields.FirstOrDefault(f =>
            string.Equals(f.DataType, "date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.DataType, "datetime", StringComparison.OrdinalIgnoreCase));

        if (dateField != null && RepositoryItemTableColumns.Has(tableColumns, dateField.SqlColumnName))
            return dateField.SqlColumnName;

        return null;
    }

    private static string? GetString(SqlDataReader reader, string column) =>
        reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));

    private static string? GetString(SqlDataReader reader, HashSet<string> columns, string column) =>
        RepositoryItemTableColumns.Has(columns, column) ? GetString(reader, column) : null;

    private static DateTime? GetDateTime(SqlDataReader reader, HashSet<string> columns, string column) =>
        RepositoryItemTableColumns.Has(columns, column) && !reader.IsDBNull(reader.GetOrdinal(column))
            ? reader.GetDateTime(reader.GetOrdinal(column))
            : null;

    private static decimal? GetDecimal(SqlDataReader reader, HashSet<string> columns, string column) =>
        RepositoryItemTableColumns.Has(columns, column) && !reader.IsDBNull(reader.GetOrdinal(column))
            ? reader.GetDecimal(reader.GetOrdinal(column))
            : null;

    private static byte? GetByte(SqlDataReader reader, HashSet<string> columns, string column) =>
        RepositoryItemTableColumns.Has(columns, column) && !reader.IsDBNull(reader.GetOrdinal(column))
            ? reader.GetByte(reader.GetOrdinal(column))
            : null;

    private static int? GetInt32(SqlDataReader reader, HashSet<string> columns, string column) =>
        RepositoryItemTableColumns.Has(columns, column) && !reader.IsDBNull(reader.GetOrdinal(column))
            ? reader.GetInt32(reader.GetOrdinal(column))
            : null;
}
