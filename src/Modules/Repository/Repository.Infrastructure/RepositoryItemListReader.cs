using System.Globalization;
using Microsoft.Data.SqlClient;
using SaaSApp.Repository.Application;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

internal static class RepositoryItemListReader
{
    private static readonly string[] OptionalListColumns =
    [
        "DocumentType", "Supplier", "InvoiceNumber", "PoNumber", "DocumentDate",
        "Amount", "Currency", "RiskLevel", "Source", "Department", "Status", "OcrScore", "AiStatus",
        "FileVersion", "InvoiceDate", "InvoiceAmount", "InvoiceTaxAmount", "PODate", "POAmount",
        "Buyer", "Terms", "SupplierAddress", "ShipToAddress", "PayToAddress"
    ];

    private static readonly string[] JsonListColumns = ["OcrJson", "SummaryJson"];

    private static readonly string[][] ScalarAliasGroups =
    [
        ["Supplier", "VendorName", "Vendor"],
        ["InvoiceNumber", "InvoiceNo"],
        ["DocumentDate", "InvoiceDate", "PODate"],
        ["Amount", "InvoiceAmount", "POAmount"],
        ["AiStatus", "MatchedStatus"],
        ["Status", "StageStatus"],
    ];

    private static readonly Dictionary<string, string> SqlColumnToListProperty =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceNumber"] = "InvoiceNumber",
            ["InvoiceNo"] = "InvoiceNumber",
            ["Supplier"] = "Supplier",
            ["VendorName"] = "Supplier",
            ["Vendor"] = "Supplier",
            ["PoNumber"] = "PoNumber",
            ["PONumber"] = "PoNumber",
            ["DocumentDate"] = "DocumentDate",
            ["InvoiceDate"] = "DocumentDate",
            ["PODate"] = "DocumentDate",
            ["Amount"] = "Amount",
            ["InvoiceAmount"] = "Amount",
            ["POAmount"] = "Amount",
            ["InvoiceTaxAmount"] = "InvoiceTaxAmount",
            ["DocumentType"] = "DocumentType",
            ["Currency"] = "Currency",
            ["Buyer"] = "Buyer",
            ["Terms"] = "Terms",
            ["SupplierAddress"] = "SupplierAddress",
            ["ShipToAddress"] = "ShipToAddress",
            ["PayToAddress"] = "PayToAddress",
            ["Status"] = "Status",
            ["StageStatus"] = "Status",
            ["OcrScore"] = "OcrScore",
            ["AiStatus"] = "AiStatus",
            ["MatchedStatus"] = "AiStatus",
            ["RiskLevel"] = "RiskLevel",
            ["Source"] = "Source",
            ["Department"] = "Department",
        };

    public static string BuildSelectList(HashSet<string> tableColumns, RepositoryDetailDto? repository = null)
    {
        var parts = new List<string> { "i.Id", "i.FileName" };
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "FileName" };

        void AddColumn(string column)
        {
            if (!RepositoryItemTableColumns.Has(tableColumns, column) || !added.Add(column))
                return;

            parts.Add($"i.[{column}]");
        }

        foreach (var col in OptionalListColumns)
            AddColumn(col);

        foreach (var group in ScalarAliasGroups)
        {
            foreach (var col in group)
                AddColumn(col);
        }

        foreach (var col in JsonListColumns)
            AddColumn(col);

        if (repository != null)
        {
            foreach (var field in repository.Fields)
            {
                if (!SqlColumnToListProperty.ContainsKey(field.SqlColumnName)
                    && !SqlColumnToListProperty.ContainsKey(field.Name))
                {
                    continue;
                }

                AddColumn(field.SqlColumnName);
            }
        }

        parts.Add("i.StorageProviderId");
        parts.Add("sp.Code");
        AddColumn("FilePath");
        AddColumn("WorkflowInstanceId");

        return string.Join(", ", parts);
    }

    public static RepositoryItemListDto ReadRow(
        SqlDataReader reader,
        HashSet<string> tableColumns,
        RepositoryDetailDto? repository = null)
    {
        var id = reader.GetGuid(reader.GetOrdinal("Id"));
        var fileName = GetString(reader, "FileName");
        var ocrJson = GetString(reader, tableColumns, "OcrJson");
        var summaryJson = GetString(reader, tableColumns, "SummaryJson");
        Guid? workflowInstanceId = null;
        if (RepositoryItemTableColumns.Has(tableColumns, "WorkflowInstanceId")
            && !reader.IsDBNull(reader.GetOrdinal("WorkflowInstanceId")))
        {
            workflowInstanceId = reader.GetGuid(reader.GetOrdinal("WorkflowInstanceId"));
        }

        var row = new RepositoryItemListDto(
            id,
            fileName,
            GetInt32(reader, tableColumns, "FileVersion"),
            CoalesceString(reader, tableColumns, "DocumentType"),
            CoalesceString(reader, tableColumns, "Supplier", "VendorName", "Vendor"),
            CoalesceString(reader, tableColumns, "InvoiceNumber", "InvoiceNo"),
            CoalesceString(reader, tableColumns, "PoNumber", "PONumber"),
            CoalesceDateTime(reader, tableColumns, "DocumentDate", "InvoiceDate", "PODate"),
            CoalesceDecimal(reader, tableColumns, "Amount", "InvoiceAmount", "POAmount"),
            CoalesceString(reader, tableColumns, "Currency"),
            CoalesceString(reader, tableColumns, "Status", "StageStatus"),
            CoalesceByte(reader, tableColumns, "OcrScore"),
            CoalesceString(reader, tableColumns, "AiStatus", "MatchedStatus"),
            CoalesceString(reader, tableColumns, "RiskLevel"),
            CoalesceString(reader, tableColumns, "Source"),
            CoalesceString(reader, tableColumns, "Department"),
            CoalesceDateTime(reader, tableColumns, "InvoiceDate", "DocumentDate"),
            CoalesceDecimal(reader, tableColumns, "InvoiceAmount", "Amount"),
            CoalesceDecimal(reader, tableColumns, "InvoiceTaxAmount"),
            CoalesceDateTime(reader, tableColumns, "PODate"),
            CoalesceDecimal(reader, tableColumns, "POAmount"),
            CoalesceString(reader, tableColumns, "Buyer"),
            CoalesceString(reader, tableColumns, "Terms"),
            CoalesceString(reader, tableColumns, "SupplierAddress"),
            CoalesceString(reader, tableColumns, "ShipToAddress"),
            CoalesceString(reader, tableColumns, "PayToAddress"),
            reader.GetGuid(reader.GetOrdinal("StorageProviderId")),
            reader.GetString(reader.GetOrdinal("Code")),
            RepositoryItemTableColumns.Has(tableColumns, "FilePath")
                && !reader.IsDBNull(reader.GetOrdinal("FilePath"))
                && !string.IsNullOrWhiteSpace(reader.GetString(reader.GetOrdinal("FilePath"))),
            workflowInstanceId);

        return RepositoryItemListMetadataEnricher.Enrich(row, ocrJson, summaryJson, repository, reader, tableColumns);
    }

    public static string? ResolveDateFilterColumn(HashSet<string> tableColumns, RepositoryDetailDto repo)
    {
        if (RepositoryItemTableColumns.Has(tableColumns, "DocumentDate"))
            return "DocumentDate";

        if (RepositoryItemTableColumns.Has(tableColumns, "InvoiceDate"))
            return "InvoiceDate";

        if (RepositoryItemTableColumns.Has(tableColumns, "PODate"))
            return "PODate";

        var dateField = repo.Fields.FirstOrDefault(f =>
            string.Equals(f.DataType, "date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.DataType, "datetime", StringComparison.OrdinalIgnoreCase));

        if (dateField != null && RepositoryItemTableColumns.Has(tableColumns, dateField.SqlColumnName))
            return dateField.SqlColumnName;

        return null;
    }

    private static string? CoalesceString(SqlDataReader reader, HashSet<string> columns, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetString(reader, columns, name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static DateTime? CoalesceDateTime(SqlDataReader reader, HashSet<string> columns, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetDateTime(reader, columns, name);
            if (value.HasValue)
                return value;
        }

        return null;
    }

    private static decimal? CoalesceDecimal(SqlDataReader reader, HashSet<string> columns, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetDecimal(reader, columns, name);
            if (value.HasValue)
                return value;
        }

        return null;
    }

    private static byte? CoalesceByte(SqlDataReader reader, HashSet<string> columns, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetByte(reader, columns, name);
            if (value.HasValue)
                return value;
        }

        return null;
    }

    private static string? GetString(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;

        var value = reader.GetValue(ordinal);
        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? null : text;

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static string? GetString(SqlDataReader reader, HashSet<string> columns, string column) =>
        RepositoryItemTableColumns.Has(columns, column) && HasColumn(reader, column) ? GetString(reader, column) : null;

    private static DateTime? GetDateTime(SqlDataReader reader, HashSet<string> columns, string column)
    {
        if (!RepositoryItemTableColumns.Has(columns, column))
            return null;
        if (!HasColumn(reader, column))
            return null;

        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;

        return TryToDateTime(reader.GetValue(ordinal));
    }

    private static decimal? GetDecimal(SqlDataReader reader, HashSet<string> columns, string column)
    {
        if (!RepositoryItemTableColumns.Has(columns, column))
            return null;
        if (!HasColumn(reader, column))
            return null;

        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;

        return TryToDecimal(reader.GetValue(ordinal));
    }

    private static byte? GetByte(SqlDataReader reader, HashSet<string> columns, string column)
    {
        if (!RepositoryItemTableColumns.Has(columns, column))
            return null;
        if (!HasColumn(reader, column))
            return null;

        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;

        return TryToByte(reader.GetValue(ordinal));
    }

    private static int? GetInt32(SqlDataReader reader, HashSet<string> columns, string column)
    {
        if (!RepositoryItemTableColumns.Has(columns, column))
            return null;
        if (!HasColumn(reader, column))
            return null;

        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;

        return TryToInt32(reader.GetValue(ordinal));
    }

    private static decimal? TryToDecimal(object value) => value switch
    {
        decimal d => d,
        double d => (decimal)d,
        float f => (decimal)f,
        int i => i,
        long l => l,
        short s => s,
        byte b => b,
        string text when TryParseDecimal(text, out var parsed) => parsed,
        _ => null
    };

    private static DateTime? TryToDateTime(object value)
    {
        if (value is DateTime dt)
            return dt;

        if (value is DateOnly date)
            return date.ToDateTime(TimeOnly.MinValue);

        if (value is not string text)
            return null;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed;

        return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out parsed)
            ? parsed
            : null;
    }

    private static byte? TryToByte(object value) => value switch
    {
        byte b => b,
        short s when s is >= 0 and <= byte.MaxValue => (byte)s,
        int i when i is >= 0 and <= byte.MaxValue => (byte)i,
        string text when byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec) => (byte)Math.Clamp(dec <= 1 ? dec * 100 : dec, 0, 255),
        _ => null
    };

    private static int? TryToInt32(object value) => value switch
    {
        int i => i,
        short s => s,
        long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
        string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null
    };

    private static bool TryParseDecimal(string text, out decimal parsed)
    {
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
            return true;

        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed);
    }

    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
