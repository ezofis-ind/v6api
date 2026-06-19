using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application.Contracts;
using System.Data;

namespace SaaSApp.Workflow.Infrastructure.Persistence;

/// <summary>
/// Repository for managing workflow-specific dynamic tables.
/// Handles transactions and CRUD operations for Comments_X, Attachments_X, etc.
/// </summary>
public sealed class DynamicTableRepository : IDynamicTableRepository
{
    private readonly ITenantContext _tenantContext;

    public DynamicTableRepository(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public string GetTableName(Guid workflowId, string entityType)
    {
        var suffix = workflowId.ToString("N").Substring(0, 8);
        return $"workflow.{entityType}_{suffix}";
    }

    public async Task<Guid> AddCommentAsync(Guid workflowId, Guid workflowInstanceId, string comments, Guid createdBy, Guid? stepInstanceId = null, string? externalCommentsBy = null, int showTo = 0, CancellationToken cancellationToken = default)
    {
        var tableName = GetTableName(workflowId, "WorkflowComments");
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var connectionString = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string is required.");
        var commentId = Guid.NewGuid();

        var sql = $@"
            INSERT INTO {tableName} 
            (Id, TenantId, WorkflowInstanceId, StepInstanceId, Comments, ExternalCommentsBy, ShowTo, CreatedBy, CreatedAtUtc, IsDeleted, EmbedStatus)
            VALUES 
            (@Id, @TenantId, @WorkflowInstanceId, @StepInstanceId, @Comments, @ExternalCommentsBy, @ShowTo, @CreatedBy, SYSUTCDATETIME(), 0, 0)";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", commentId);
        command.Parameters.AddWithValue("@TenantId", tenantId);
        command.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);
        command.Parameters.AddWithValue("@StepInstanceId", (object?)stepInstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Comments", comments);
        command.Parameters.AddWithValue("@ExternalCommentsBy", (object?)externalCommentsBy ?? DBNull.Value);
        command.Parameters.AddWithValue("@ShowTo", showTo);
        command.Parameters.AddWithValue("@CreatedBy", createdBy);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return commentId;
    }

    public async Task<Guid> AddAttachmentAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        string fileName,
        string filePath,
        Guid createdBy,
        long? fileSize = null,
        string? contentType = null,
        Guid? stepInstanceId = null,
        Guid? repositoryId = null,
        Guid? itemId = null,
        string? formJsonId = null,
        CancellationToken cancellationToken = default)
    {
        var tableName = GetTableName(workflowId, "WorkflowAttachments");
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var connectionString = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string is required.");

        var attachmentId = Guid.NewGuid();
        var itemGuid = itemId ?? TryParseGuid(formJsonId);
        var sql = $@"
            INSERT INTO {tableName}
            (Id, TenantId, WorkflowInstanceId, StepInstanceId, RepositoryId, ItemId, FormJsonId,
             FileName, FilePath, FileSize, ContentType, CreatedBy, ModifiedBy, CreatedAtUtc, ModifiedAtUtc, IsDeleted)
            VALUES
            (@Id, @TenantId, @WorkflowInstanceId, @StepInstanceId, @RepositoryId, @ItemId, @FormJsonId,
             @FileName, @FilePath, @FileSize, @ContentType, @CreatedBy, @ModifiedBy, SYSUTCDATETIME(), SYSUTCDATETIME(), 0)";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", attachmentId);
        command.Parameters.AddWithValue("@TenantId", tenantId);
        command.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);
        command.Parameters.AddWithValue("@StepInstanceId", (object?)stepInstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue("@RepositoryId", (object?)repositoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ItemId", (object?)itemGuid ?? DBNull.Value);
        command.Parameters.AddWithValue("@FormJsonId", (object?)(formJsonId ?? itemGuid?.ToString("N")) ?? DBNull.Value);
        command.Parameters.AddWithValue("@FileName", fileName);
        command.Parameters.AddWithValue("@FilePath", filePath);
        command.Parameters.AddWithValue("@FileSize", (object?)fileSize ?? DBNull.Value);
        command.Parameters.AddWithValue("@ContentType", (object?)contentType ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedBy", createdBy);
        command.Parameters.AddWithValue("@ModifiedBy", createdBy);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return attachmentId;
    }

    public async Task<IReadOnlyList<WorkflowCommentRowDto>> GetCommentsAsync(Guid workflowId, Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var tableName = GetTableName(workflowId, "WorkflowComments");
        var connectionString = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string is required.");

        var sql = $@"
            SELECT 
                Id, WorkflowInstanceId, StepInstanceId, Comments, ExternalCommentsBy, 
                ShowTo, EmbedJson, EmbedStatus, CreatedAtUtc, CreatedBy
            FROM {tableName}
            WHERE WorkflowInstanceId = @WorkflowInstanceId AND IsDeleted = 0
            ORDER BY CreatedAtUtc DESC";

        var results = new List<WorkflowCommentRowDto>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new WorkflowCommentRowDto(
                Id: reader.GetGuid(0),
                WorkflowInstanceId: reader.GetGuid(1),
                StepInstanceId: reader.IsDBNull(2) ? null : reader.GetGuid(2),
                Comments: reader.GetString(3),
                ExternalCommentsBy: reader.IsDBNull(4) ? null : reader.GetString(4),
                ShowTo: reader.GetInt32(5),
                EmbedJson: reader.IsDBNull(6) ? null : reader.GetString(6),
                EmbedStatus: reader.GetBoolean(7),
                CreatedAtUtc: reader.GetDateTime(8),
                CreatedBy: reader.GetGuid(9)));
        }

        return results;
    }

    public async Task<IReadOnlyList<WorkflowAttachmentRowDto>> GetAttachmentsAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var tableName = GetTableName(workflowId, "WorkflowAttachments");
        var connectionString = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string is required.");

        var sql = $@"
            SELECT
                Id, WorkflowInstanceId, FileName, FilePath, FileSize, ContentType,
                CreatedAtUtc, CreatedBy, ModifiedAtUtc, ModifiedBy,
                RepositoryId, ItemId, FormJsonId
            FROM {tableName}
            WHERE WorkflowInstanceId = @WorkflowInstanceId AND IsDeleted = 0
            ORDER BY CreatedAtUtc DESC";

        var results = new List<WorkflowAttachmentRowDto>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(MapAttachmentRow(reader));

        return results;
    }

    private static WorkflowAttachmentRowDto MapAttachmentRow(SqlDataReader reader)
    {
        var formJsonId = GetNullableString(reader, "FormJsonId");
        var repositoryId = ReadRepositoryOrItemGuid(reader, "RepositoryId");
        var itemId = ReadRepositoryOrItemGuid(reader, "ItemId") ?? TryParseGuid(formJsonId);

        return new WorkflowAttachmentRowDto(
            Id: reader.GetGuid(reader.GetOrdinal("Id")),
            WorkflowInstanceId: reader.GetGuid(reader.GetOrdinal("WorkflowInstanceId")),
            FileName: GetNullableString(reader, "FileName"),
            FilePath: GetNullableString(reader, "FilePath"),
            FileSize: GetNullableInt64(reader, "FileSize"),
            ContentType: GetNullableString(reader, "ContentType"),
            CreatedAtUtc: reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
            CreatedBy: reader.GetGuid(reader.GetOrdinal("CreatedBy")),
            ModifiedAtUtc: GetNullableDateTime(reader, "ModifiedAtUtc"),
            ModifiedBy: GetNullableGuid(reader, "ModifiedBy"),
            RepositoryId: repositoryId,
            ItemId: itemId,
            FormJsonId: formJsonId);
    }

    private static Guid? ReadRepositoryOrItemGuid(SqlDataReader reader, string column)
    {
        var i = reader.GetOrdinal(column);
        if (reader.IsDBNull(i))
            return null;

        return reader.GetFieldType(i) == typeof(Guid)
            ? reader.GetGuid(i)
            : TryParseGuid(reader.GetValue(i)?.ToString());
    }

    private static Guid? TryParseGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (Guid.TryParse(trimmed, out var guid))
            return guid;

        return trimmed.Length == 32 && Guid.TryParseExact(trimmed, "N", out guid) ? guid : null;
    }

    private static string? GetNullableString(SqlDataReader reader, string column)
    {
        var i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetString(i);
    }

    private static long? GetNullableInt64(SqlDataReader reader, string column)
    {
        var i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetInt64(i);
    }

    private static int? GetNullableInt32(SqlDataReader reader, string column)
    {
        var i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetInt32(i);
    }

    private static DateTime? GetNullableDateTime(SqlDataReader reader, string column)
    {
        var i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetDateTime(i);
    }

    private static Guid? GetNullableGuid(SqlDataReader reader, string column)
    {
        var i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetGuid(i);
    }
}
