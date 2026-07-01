using Microsoft.Data.SqlClient;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure;

internal sealed class RepositoryStageRow
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid RepositoryId { get; init; }
    public Guid StorageProviderId { get; init; }
    public string? FilePath { get; init; }
    public string? FileName { get; init; }
    public string? FileType { get; init; }
    public int? FileSize { get; init; }
    public string StageStatus { get; init; } = "Pending";
    public string? Status { get; init; }
    public Guid? PromotedItemId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ModifiedAtUtc { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? ModifiedBy { get; init; }
    public bool IsDeleted { get; init; }
    public string? OcrJson { get; init; }
    public string? SummaryJson { get; init; }
    public Dictionary<string, string> FieldValues { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class RepositoryStageStore
{
    private static readonly string[] CoreColumns =
    [
        "Id", "TenantId", "RepositoryId", "FolderId", "StorageProviderId",
        "FilePath", "FileName", "FileType", "FileSize", "TotalPages",
        "StageStatus", "Status", "MailId", "OcrScore", "AiStatus",
        "OcrText", "OcrJson", "SummaryJson", "PromotedItemId",
        "CreatedAtUtc", "ModifiedAtUtc", "CreatedBy", "ModifiedBy", "IsDeleted"
    ];

    public static async Task<Guid> InsertAsync(
        SqlConnection connection,
        RepositoryDetailDto repo,
        Guid tenantId,
        Guid repositoryId,
        Guid storageProviderId,
        string relativePath,
        string fileName,
        string? contentType,
        int? fileSize,
        IReadOnlyDictionary<string, string>? fieldValues,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var stageId = Guid.NewGuid();
        var table = RepositorySqlHelper.QualifiedItemsTable(repo.StageTableName);
        var tableColumns = await RepositoryItemTableColumns.LoadAsync(connection, repo.StageTableName, cancellationToken);
        var allowedColumns = RepositoryItemFilterHelper.BuildFilterableColumns(repo, tableColumns);

        var columns = new List<string>();
        var values = new List<string>();
        var parameters = new List<SqlParameter>();

        void Add(string column, string param, object? value)
        {
            if (!RepositoryItemTableColumns.Has(tableColumns, column))
                return;
            columns.Add($"[{column}]");
            values.Add(param);
            parameters.Add(new SqlParameter(param, value ?? DBNull.Value));
        }

        Add("Id", "@Id", stageId);
        Add("TenantId", "@TenantId", tenantId);
        Add("RepositoryId", "@RepositoryId", repositoryId);
        Add("StorageProviderId", "@StorageProviderId", storageProviderId);
        Add("FilePath", "@FilePath", relativePath);
        Add("FileName", "@FileName", fileName);
        Add("FileType", "@FileType", contentType);
        Add("FileSize", "@FileSize", fileSize);
        Add("StageStatus", "@StageStatus", "Uploaded");
        Add("Status", "@Status", "Pending");
        Add("CreatedBy", "@CreatedBy", userId);

        var used = new HashSet<string>(columns.Select(c => c.Trim('[', ']')), StringComparer.OrdinalIgnoreCase);
        var fieldIndex = 0;
        foreach (var (key, value) in fieldValues ?? new Dictionary<string, string>())
        {
            if (!RepositoryItemFilterHelper.TryResolveFilterColumn(key, allowedColumns, repo, out var col))
                continue;
            if (!RepositoryItemTableColumns.Has(tableColumns, col) || !used.Add(col))
                continue;

            var param = $"@F{fieldIndex++}";
            columns.Add($"[{col}]");
            values.Add(param);
            parameters.Add(new SqlParameter(param, value));
        }

        var sql = $"""
            INSERT INTO {table} ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)});
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddRange(parameters.ToArray());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return stageId;
    }

    public static async Task<RepositoryStageRow?> GetAsync(
        SqlConnection connection,
        RepositoryDetailDto repo,
        Guid tenantId,
        Guid stageId,
        CancellationToken cancellationToken)
    {
        var table = RepositorySqlHelper.QualifiedItemsTable(repo.StageTableName);
        var tableColumns = await RepositoryItemTableColumns.LoadAsync(connection, repo.StageTableName, cancellationToken);
        var selectCols = tableColumns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        if (selectCols.Count == 0)
            return null;

        var sql = $"""
            SELECT {string.Join(", ", selectCols.Select(c => $"[{c}]"))}
            FROM {table}
            WHERE Id = @Id AND TenantId = @TenantId AND RepositoryId = @RepositoryId AND IsDeleted = 0;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", stageId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        cmd.Parameters.AddWithValue("@RepositoryId", repo.Id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var coreSet = new HashSet<string>(CoreColumns, StringComparer.OrdinalIgnoreCase);
        var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < selectCols.Count; i++)
        {
            var col = selectCols[i];
            var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
            values[col] = val;
            if (!coreSet.Contains(col) && val != null)
                fieldValues[col] = Convert.ToString(val) ?? string.Empty;
        }

        return new RepositoryStageRow
        {
            Id = GetGuid(values, "Id"),
            TenantId = GetGuid(values, "TenantId"),
            RepositoryId = GetGuid(values, "RepositoryId"),
            StorageProviderId = GetGuid(values, "StorageProviderId"),
            FilePath = GetString(values, "FilePath"),
            FileName = GetString(values, "FileName"),
            FileType = GetString(values, "FileType"),
            FileSize = GetInt(values, "FileSize"),
            StageStatus = GetString(values, "StageStatus") ?? "Pending",
            Status = GetString(values, "Status"),
            PromotedItemId = GetNullableGuid(values, "PromotedItemId"),
            CreatedAtUtc = GetDateTime(values, "CreatedAtUtc") ?? DateTime.UtcNow,
            ModifiedAtUtc = GetDateTime(values, "ModifiedAtUtc"),
            CreatedBy = GetNullableGuid(values, "CreatedBy"),
            ModifiedBy = GetNullableGuid(values, "ModifiedBy"),
            IsDeleted = GetBool(values, "IsDeleted"),
            OcrJson = GetString(values, "OcrJson"),
            SummaryJson = GetString(values, "SummaryJson"),
            FieldValues = fieldValues
        };
    }

    public static async Task UpdateFieldsAsync(
        SqlConnection connection,
        RepositoryDetailDto repo,
        Guid tenantId,
        Guid stageId,
        IReadOnlyDictionary<string, string> fieldValues,
        string? status,
        string? stageStatus,
        string? ocrResult,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var table = RepositorySqlHelper.QualifiedItemsTable(repo.StageTableName);
        var tableColumns = await RepositoryItemTableColumns.LoadAsync(connection, repo.StageTableName, cancellationToken);
        var allowedColumns = RepositoryItemFilterHelper.BuildFilterableColumns(repo, tableColumns);
        var updates = new List<string>();
        var parameters = new List<SqlParameter>
        {
            new("@Id", stageId),
            new("@TenantId", tenantId),
            new("@RepositoryId", repo.Id)
        };

        var index = 0;
        foreach (var (key, value) in fieldValues)
        {
            if (!RepositoryItemFilterHelper.TryResolveFilterColumn(key, allowedColumns, repo, out var col))
                continue;
            if (!RepositoryItemTableColumns.Has(tableColumns, col))
                continue;

            var param = $"@U{index++}";
            updates.Add($"[{col}] = {param}");
            parameters.Add(new SqlParameter(param, value));
        }

        if (!string.IsNullOrWhiteSpace(status) && RepositoryItemTableColumns.Has(tableColumns, "Status"))
            updates.Add("[Status] = @Status");
        if (!string.IsNullOrWhiteSpace(stageStatus) && RepositoryItemTableColumns.Has(tableColumns, "StageStatus"))
            updates.Add("[StageStatus] = @StageStatus");
        if (!string.IsNullOrWhiteSpace(ocrResult) && RepositoryItemTableColumns.Has(tableColumns, "OcrJson"))
            updates.Add("[OcrJson] = @OcrJson");

        updates.Add("[ModifiedAtUtc] = SYSUTCDATETIME()");
        if (RepositoryItemTableColumns.Has(tableColumns, "ModifiedBy"))
            updates.Add("[ModifiedBy] = @ModifiedBy");

        if (!string.IsNullOrWhiteSpace(status))
            parameters.Add(new SqlParameter("@Status", status));
        if (!string.IsNullOrWhiteSpace(stageStatus))
            parameters.Add(new SqlParameter("@StageStatus", stageStatus));
        if (!string.IsNullOrWhiteSpace(ocrResult))
            parameters.Add(new SqlParameter("@OcrJson", ocrResult));
        parameters.Add(new SqlParameter("@ModifiedBy", (object?)userId ?? DBNull.Value));

        var sql = $"""
            UPDATE {table}
            SET {string.Join(", ", updates)}
            WHERE Id = @Id AND TenantId = @TenantId AND RepositoryId = @RepositoryId AND IsDeleted = 0;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddRange(parameters.ToArray());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task MarkArchivedAsync(
        SqlConnection connection,
        string stageTableName,
        Guid tenantId,
        Guid stageId,
        Guid promotedItemId,
        CancellationToken cancellationToken)
    {
        var table = RepositorySqlHelper.QualifiedItemsTable(stageTableName);
        var sql = $"""
            UPDATE {table}
            SET PromotedItemId = @PromotedItemId,
                StageStatus = 'Archived',
                Status = 'ARCHIVED',
                ModifiedAtUtc = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", stageId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        cmd.Parameters.AddWithValue("@PromotedItemId", promotedItemId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<(IReadOnlyList<RepositoryStageRow> Items, int Total)> ListAsync(
        SqlConnection connection,
        RepositoryDetailDto repo,
        Guid tenantId,
        bool includeDeleted,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var table = RepositorySqlHelper.QualifiedItemsTable(repo.StageTableName);
        var where = includeDeleted
            ? "TenantId = @TenantId AND RepositoryId = @RepositoryId"
            : "TenantId = @TenantId AND RepositoryId = @RepositoryId AND IsDeleted = 0 AND ISNULL(Status, '') <> 'ARCHIVED'";

        var countSql = $"SELECT COUNT(*) FROM {table} WHERE {where};";
        await using var countCmd = new SqlCommand(countSql, connection);
        countCmd.Parameters.AddWithValue("@TenantId", tenantId);
        countCmd.Parameters.AddWithValue("@RepositoryId", repo.Id);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken));

        var sql = $"""
            SELECT Id
            FROM {table}
            WHERE {where}
            ORDER BY CreatedAtUtc DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        var ids = new List<Guid>();
        await using (var listCmd = new SqlCommand(sql, connection))
        {
            listCmd.Parameters.AddWithValue("@TenantId", tenantId);
            listCmd.Parameters.AddWithValue("@RepositoryId", repo.Id);
            listCmd.Parameters.AddWithValue("@Skip", Math.Max(skip, 0));
            listCmd.Parameters.AddWithValue("@Take", Math.Max(take, 1));
            await using var reader = await listCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetGuid(0));
        }

        var items = new List<RepositoryStageRow>();
        foreach (var id in ids)
        {
            var row = await GetAsync(connection, repo, tenantId, id, cancellationToken);
            if (row != null)
                items.Add(row);
        }

        return (items, total);
    }

    private static Guid GetGuid(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var v) && v is Guid g ? g : Guid.Empty;

    private static Guid? GetNullableGuid(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var v) && v is Guid g ? g : null;

    private static string? GetString(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var v) ? Convert.ToString(v) : null;

    private static int? GetInt(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var v) && v != null && int.TryParse(v.ToString(), out var n) ? n : null;

    private static DateTime? GetDateTime(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var v) && v is DateTime dt ? dt : null;

    private static bool GetBool(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var v) && v is bool b && b;
}
