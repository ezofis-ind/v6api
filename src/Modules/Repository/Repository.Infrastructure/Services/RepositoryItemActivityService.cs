using Microsoft.Data.SqlClient;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure.Services;

public sealed class RepositoryItemActivityService : IRepositoryItemActivityService
{
    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly IStaticRepositoryProvisioner _provisioner;

    public RepositoryItemActivityService(
        ITenantConnectionProvider connectionProvider,
        IStaticRepositoryProvisioner provisioner)
    {
        _connectionProvider = connectionProvider;
        _provisioner = provisioner;
    }

    public async Task<RepositoryItemTimelineResultDto?> GetTimelineAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (!await ItemExistsAsync(repositoryId, tenantId, itemId, cancellationToken))
            return null;

        var connectionString = RequireConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var events = new List<RepositoryItemTimelineEventDto>();
        if (await TimelineTableExistsAsync(connection, cancellationToken))
        {
            const string sql = """
                SELECT Id, EventType, Title, Description, ActorType, ActorName, CreatedAtUtc
                FROM repository.ItemTimelineEvents
                WHERE RepositoryId = @RepositoryId AND ItemId = @ItemId AND TenantId = @TenantId AND IsDeleted = 0
                ORDER BY CreatedAtUtc ASC;
                """;

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@TenantId", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new RepositoryItemTimelineEventDto(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetDateTime(6)));
            }
        }

        if (events.Count == 0)
        {
            var fields = await LoadItemFieldsForTimelineAsync(repositoryId, tenantId, itemId, cancellationToken);
            if (fields != null)
                events.AddRange(RepositoryItemTimelineDeriver.Derive(fields));
        }

        return new RepositoryItemTimelineResultDto(events, events.Count);
    }

    public async Task<RepositoryItemTimelineEventDto?> AddTimelineEventAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        AddRepositoryItemTimelineEventRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.");

        if (!await ItemExistsAsync(repositoryId, tenantId, itemId, cancellationToken))
            return null;

        var eventType = string.IsNullOrWhiteSpace(request.EventType) ? "user" : request.EventType.Trim();
        var actorName = request.ActorName;
        if (string.IsNullOrWhiteSpace(actorName) && userId.HasValue)
            actorName = userId.Value.ToString("D");

        var connectionString = RequireConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        if (!await TimelineTableExistsAsync(connection, cancellationToken))
            throw new InvalidOperationException("Timeline is not enabled for this tenant database. Apply repository schema or run CreateRepositorySchema.sql.");

        var id = Guid.NewGuid();
        await RecordTimelineEventInternalAsync(
            id,
            repositoryId,
            tenantId,
            itemId,
            eventType,
            request.Title.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            eventType.Equals("system", StringComparison.OrdinalIgnoreCase) ? "System" : "User",
            actorName,
            userId,
            userId,
            cancellationToken);

        return new RepositoryItemTimelineEventDto(
            id,
            eventType,
            request.Title.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            eventType.Equals("system", StringComparison.OrdinalIgnoreCase) ? "System" : "User",
            actorName,
            DateTime.UtcNow);
    }

    public Task RecordTimelineEventAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        string eventType,
        string title,
        string? description,
        string actorType,
        string? actorName,
        Guid? actorUserId,
        Guid? createdBy,
        CancellationToken cancellationToken = default) =>
        RecordTimelineEventInternalAsync(
            Guid.NewGuid(),
            repositoryId,
            tenantId,
            itemId,
            eventType,
            title,
            description,
            actorType,
            actorName,
            actorUserId,
            createdBy,
            cancellationToken);

    public async Task<RepositoryItemCommentsResultDto?> GetCommentsAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await ItemExistsAsync(repositoryId, tenantId, itemId, cancellationToken))
            return null;

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var connectionString = RequireConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await CommentsTableExistsAsync(connection, cancellationToken))
            return new RepositoryItemCommentsResultDto(Array.Empty<RepositoryItemCommentDto>(), 0, page, pageSize);

        const string countSql = """
            SELECT COUNT(1)
            FROM repository.ItemComments
            WHERE RepositoryId = @RepositoryId AND ItemId = @ItemId AND TenantId = @TenantId AND IsDeleted = 0;
            """;

        await using (var countCmd = new SqlCommand(countSql, connection))
        {
            countCmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
            countCmd.Parameters.AddWithValue("@ItemId", itemId);
            countCmd.Parameters.AddWithValue("@TenantId", tenantId);
            var total = (int)(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0);

            const string sql = """
                SELECT Id, Body, CreatedBy, CreatedAtUtc, ModifiedAtUtc
                FROM repository.ItemComments
                WHERE RepositoryId = @RepositoryId AND ItemId = @ItemId AND TenantId = @TenantId AND IsDeleted = 0
                ORDER BY CreatedAtUtc DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
                """;

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@TenantId", tenantId);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);

            var comments = new List<RepositoryItemCommentDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                comments.Add(new RepositoryItemCommentDto(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetGuid(2),
                    reader.GetDateTime(3),
                    reader.IsDBNull(4) ? null : reader.GetDateTime(4)));
            }

            return new RepositoryItemCommentsResultDto(comments, total, page, pageSize);
        }
    }

    public async Task<AddRepositoryItemCommentResult?> AddCommentAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        AddRepositoryItemCommentRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("Comment body is required.");

        if (!await ItemExistsAsync(repositoryId, tenantId, itemId, cancellationToken))
            return null;

        var commentId = Guid.NewGuid();
        var connectionString = RequireConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await CommentsTableExistsAsync(connection, cancellationToken))
            throw new InvalidOperationException("Comments are not enabled for this tenant database. Apply repository schema or run CreateRepositorySchema.sql.");

        const string sql = """
            INSERT INTO repository.ItemComments (Id, TenantId, RepositoryId, ItemId, Body, CreatedBy)
            VALUES (@Id, @TenantId, @RepositoryId, @ItemId, @Body, @CreatedBy);
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", commentId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        cmd.Parameters.AddWithValue("@Body", request.Body.Trim());
        cmd.Parameters.AddWithValue("@CreatedBy", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new AddRepositoryItemCommentResult(commentId);
    }

    private async Task RecordTimelineEventInternalAsync(
        Guid id,
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        string eventType,
        string title,
        string? description,
        string actorType,
        string? actorName,
        Guid? actorUserId,
        Guid? createdBy,
        CancellationToken cancellationToken)
    {
        var connectionString = RequireConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TimelineTableExistsAsync(connection, cancellationToken))
            return;

        const string sql = """
            INSERT INTO repository.ItemTimelineEvents
                (Id, TenantId, RepositoryId, ItemId, EventType, Title, Description, ActorType, ActorName, ActorUserId, CreatedBy)
            VALUES
                (@Id, @TenantId, @RepositoryId, @ItemId, @EventType, @Title, @Description, @ActorType, @ActorName, @ActorUserId, @CreatedBy);
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        cmd.Parameters.AddWithValue("@EventType", eventType);
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ActorType", actorType);
        cmd.Parameters.AddWithValue("@ActorName", (object?)actorName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ActorUserId", (object?)actorUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", (object?)createdBy ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, object?>?> LoadItemFieldsForTimelineAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var repo = await _provisioner.GetRepositoryAsync(repositoryId, tenantId, cancellationToken);
        if (repo == null)
            return null;

        var table = RepositorySqlHelper.QualifiedItemsTable(repo.ItemsTableName);
        var connectionString = RequireConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            SELECT CreatedAtUtc, Source, OcrScore, AiStatus, MatchedStatus
            FROM {table}
            WHERE Id = @ItemId AND RepositoryId = @RepositoryId AND TenantId = @TenantId AND IsDeleted = 0;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CreatedAtUtc"] = reader.IsDBNull(0) ? null : reader.GetValue(0),
            ["Source"] = reader.IsDBNull(1) ? null : reader.GetValue(1),
            ["OcrScore"] = reader.IsDBNull(2) ? null : reader.GetValue(2),
            ["AiStatus"] = reader.IsDBNull(3) ? null : reader.GetValue(3),
            ["MatchedStatus"] = reader.IsDBNull(4) ? null : reader.GetValue(4),
        };
    }

    private async Task<bool> ItemExistsAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var repo = await _provisioner.GetRepositoryAsync(repositoryId, tenantId, cancellationToken);
        if (repo == null)
            throw new InvalidOperationException("Repository not found.");

        var table = RepositorySqlHelper.QualifiedItemsTable(repo.ItemsTableName);
        var connectionString = RequireConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"SELECT 1 FROM {table} WHERE Id = @ItemId AND RepositoryId = @RepositoryId AND TenantId = @TenantId AND IsDeleted = 0;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ItemId", itemId);
        cmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        return await cmd.ExecuteScalarAsync(cancellationToken) != null;
    }

    private string RequireConnectionString() =>
        _connectionProvider.ConnectionString
        ?? throw new InvalidOperationException("Tenant connection string not resolved.");

    private static Task<bool> TimelineTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        TableExistsAsync(connection, "repository", "ItemTimelineEvents", cancellationToken);

    private static Task<bool> CommentsTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        TableExistsAsync(connection, "repository", "ItemComments", cancellationToken);

    private static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1 FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @Schema AND t.name = @Table;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);
        return await cmd.ExecuteScalarAsync(cancellationToken) != null;
    }
}
