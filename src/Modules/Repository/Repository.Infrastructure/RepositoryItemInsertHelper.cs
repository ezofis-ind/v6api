using Microsoft.Data.SqlClient;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

internal static class RepositoryItemInsertHelper
{
    private static readonly (string Column, Func<CreateRepositoryItemRequest, object?> Value)[] OptionalCoreColumns =
    [
        ("Status", r => r.Status),
        ("OcrScore", r => r.OcrPercent),
        ("AiStatus", r => r.AiStatus),
        ("WorkflowInstanceId", r => r.WorkflowInstanceId),
        ("FileVersion", r => r.FileVersion ?? 1),
    ];

    public static async Task InsertItemAsync(
        SqlConnection connection,
        RepositoryDetailDto repo,
        Guid tenantId,
        Guid repositoryId,
        Guid itemId,
        Guid storageProviderId,
        CreateRepositoryItemRequest request,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var table = RepositorySqlHelper.QualifiedItemsTable(repo.ItemsTableName);
        var tableColumns = await RepositoryItemTableColumns.LoadAsync(connection, repo.ItemsTableName, cancellationToken);
        var allowedColumns = RepositoryItemFilterHelper.BuildFilterableColumns(repo, tableColumns);
        var metadata = request.FieldValues ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        request = RepositoryItemMetadataMerger.Apply(request, metadata);
        var fieldValues = request.FieldValues ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var columns = new List<string>();
        var values = new List<string>();
        var parameters = new List<SqlParameter>();

        void AddIfExists(string column, string param, object? value)
        {
            if (!RepositoryItemTableColumns.Has(tableColumns, column))
                return;

            columns.Add($"[{column}]");
            values.Add(param);
            parameters.Add(new SqlParameter(param, value ?? DBNull.Value));
        }

        AddIfExists("Id", "@Id", itemId);
        AddIfExists("TenantId", "@TenantId", tenantId);
        AddIfExists("RepositoryId", "@RepositoryId", repositoryId);
        AddIfExists("FolderId", "@FolderId", request.FolderId);
        AddIfExists("StorageProviderId", "@StorageProviderId", storageProviderId);
        AddIfExists("FilePath", "@FilePath", request.FilePath);
        AddIfExists("FileName", "@FileName", request.FileName);
        AddIfExists("FileType", "@FileType", request.FileType);
        AddIfExists("FileSize", "@FileSize", request.FileSize);
        AddIfExists("CreatedBy", "@CreatedBy", userId);

        foreach (var (column, getValue) in OptionalCoreColumns)
            AddIfExists(column, $"@{column}", getValue(request));

        var usedColumns = new HashSet<string>(
            columns.Select(c => c.Trim('[', ']')),
            StringComparer.OrdinalIgnoreCase);

        var extraIndex = 0;
        foreach (var (key, value) in fieldValues)
        {
            if (!RepositoryItemFilterHelper.TryResolveFilterColumn(key, allowedColumns, repo, out var col))
                continue;

            if (!RepositoryItemTableColumns.Has(tableColumns, col) || !usedColumns.Add(col))
                continue;

            var param = $"@F{extraIndex++}";
            columns.Add($"[{col}]");
            values.Add(param);
            parameters.Add(new SqlParameter(param, value));
        }

        if (columns.Count == 0)
            throw new InvalidOperationException("No insertable columns resolved for repository item.");

        var sql = $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});";
        await using var cmd = new SqlCommand(sql, connection);
        foreach (var p in parameters)
            cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
