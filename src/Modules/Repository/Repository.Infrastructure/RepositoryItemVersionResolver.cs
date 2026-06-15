using Microsoft.Data.SqlClient;
using SaaSApp.Repository.Infrastructure.Storage;

namespace SaaSApp.Repository.Infrastructure;

internal static class RepositoryItemVersionResolver
{
    public static async Task EnsureFileVersionColumnAsync(
        SqlConnection connection,
        string itemsTableName,
        CancellationToken cancellationToken)
    {
        if (!RepositorySqlHelper.IsValidItemsTableName(itemsTableName))
            return;

        var tableColumns = await RepositoryItemTableColumns.LoadAsync(connection, itemsTableName, cancellationToken);
        if (RepositoryItemTableColumns.Has(tableColumns, "FileVersion"))
            return;

        var constraint = $"DF_{itemsTableName.Replace("-", "_")}_FileVersion";
        var sql = $"""
            IF COL_LENGTH('repository.{itemsTableName}', 'FileVersion') IS NULL
            BEGIN
                ALTER TABLE repository.[{itemsTableName}]
                    ADD [FileVersion] INT NOT NULL CONSTRAINT {constraint} DEFAULT (1);
            END
            """;

        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Next version for the same repository folder + file name (1 = first upload).
    /// </summary>
    public static async Task<int> ResolveNextFileVersionAsync(
        SqlConnection connection,
        string itemsTableName,
        Guid tenantId,
        Guid repositoryId,
        Guid? folderId,
        string fileName,
        CancellationToken cancellationToken)
    {
        await EnsureFileVersionColumnAsync(connection, itemsTableName, cancellationToken);

        var tableColumns = await RepositoryItemTableColumns.LoadAsync(connection, itemsTableName, cancellationToken);
        if (!RepositoryItemTableColumns.Has(tableColumns, "FileVersion"))
            return 1;

        var baseFileName = RepositoryFilePathHelper.GetBaseFileName(fileName);
        if (string.IsNullOrWhiteSpace(baseFileName))
            return 1;

        var versionedLike = RepositoryFilePathHelper.BuildVersionedFileNameLikePattern(baseFileName);

        var table = RepositorySqlHelper.QualifiedItemsTable(itemsTableName);
        var sql = $"""
            SELECT MAX(CAST(ISNULL([FileVersion], 1) AS INT))
            FROM {table}
            WHERE TenantId = @TenantId
              AND RepositoryId = @RepositoryId
              AND IsDeleted = 0
              AND (
                    [FileName] = @BaseFileName
                 OR [FileName] LIKE @VersionedLike
              )
              AND (
                    (@FolderId IS NULL AND FolderId IS NULL)
                 OR FolderId = @FolderId
              );
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        cmd.Parameters.AddWithValue("@BaseFileName", baseFileName);
        cmd.Parameters.AddWithValue("@VersionedLike", versionedLike);
        cmd.Parameters.AddWithValue("@FolderId", (object?)folderId ?? DBNull.Value);

        var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
        if (scalar is null or DBNull)
            return 1;

        var max = Convert.ToInt32(scalar);
        return max < 1 ? 1 : max + 1;
    }
}
