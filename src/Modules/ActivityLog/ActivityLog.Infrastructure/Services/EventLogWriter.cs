using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaaSApp.ActivityLog.Application.Contracts;

namespace SaaSApp.ActivityLog.Infrastructure.Services;

public sealed class EventLogWriter : IEventLogWriter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventLogWriter> _logger;

    public EventLogWriter(IServiceScopeFactory scopeFactory, ILogger<EventLogWriter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(EventLogEntry entry, string connectionString)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var inserter = scope.ServiceProvider.GetRequiredService<EventLogInsertService>();
                await inserter.InsertAsync(entry, connectionString, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write event log for {Path}", entry.Path);
            }
        });
    }
}

public sealed class EventLogInsertService
{
    public async Task InsertAsync(EventLogEntry entry, string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureEventLogsTableAsync(connection, cancellationToken);

        const string sql = """
            INSERT INTO activitylog.EventLogs (
                Id, TenantId, UserId, UserDisplayName, UserEmail,
                EventTitle, EventType, Category, Severity,
                IpAddress, HttpMethod, Path, StatusCode, CorrelationId, CreatedAtUtc)
            VALUES (
                @Id, @TenantId, @UserId, @UserDisplayName, @UserEmail,
                @EventTitle, @EventType, @Category, @Severity,
                @IpAddress, @HttpMethod, @Path, @StatusCode, @CorrelationId, @CreatedAtUtc)
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", entry.Id);
        command.Parameters.AddWithValue("@TenantId", entry.TenantId);
        command.Parameters.AddWithValue("@UserId", (object?)entry.UserId ?? DBNull.Value);
        command.Parameters.AddWithValue("@UserDisplayName", (object?)entry.UserDisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("@UserEmail", (object?)entry.UserEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("@EventTitle", entry.EventTitle);
        command.Parameters.AddWithValue("@EventType", entry.EventType);
        command.Parameters.AddWithValue("@Category", entry.Category);
        command.Parameters.AddWithValue("@Severity", entry.Severity);
        command.Parameters.AddWithValue("@IpAddress", (object?)entry.IpAddress ?? DBNull.Value);
        command.Parameters.AddWithValue("@HttpMethod", (object?)entry.HttpMethod ?? DBNull.Value);
        command.Parameters.AddWithValue("@Path", (object?)entry.Path ?? DBNull.Value);
        command.Parameters.AddWithValue("@StatusCode", (object?)entry.StatusCode ?? DBNull.Value);
        command.Parameters.AddWithValue("@CorrelationId", (object?)entry.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAtUtc", entry.CreatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureEventLogsTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'activitylog')
                EXEC(N'CREATE SCHEMA activitylog');

            IF OBJECT_ID(N'activitylog.EventLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE activitylog.EventLogs (
                    Id               uniqueidentifier NOT NULL CONSTRAINT PK_EventLogs PRIMARY KEY,
                    TenantId         uniqueidentifier NOT NULL,
                    UserId           uniqueidentifier NULL,
                    UserDisplayName  nvarchar(256) NULL,
                    UserEmail        nvarchar(256) NULL,
                    EventTitle       nvarchar(512) NOT NULL,
                    EventType        nvarchar(128) NOT NULL,
                    Category         nvarchar(64) NOT NULL,
                    Severity         nvarchar(32) NOT NULL,
                    IpAddress        nvarchar(64) NULL,
                    HttpMethod       nvarchar(10) NULL,
                    Path             nvarchar(512) NULL,
                    StatusCode       int NULL,
                    CorrelationId    nvarchar(64) NULL,
                    CreatedAtUtc     datetime2 NOT NULL
                );

                CREATE INDEX IX_EventLogs_TenantId_CreatedAtUtc
                    ON activitylog.EventLogs (TenantId, CreatedAtUtc DESC);

                CREATE INDEX IX_EventLogs_TenantId_Category_CreatedAtUtc
                    ON activitylog.EventLogs (TenantId, Category, CreatedAtUtc DESC);

                CREATE INDEX IX_EventLogs_TenantId_Severity
                    ON activitylog.EventLogs (TenantId, Severity);
            END
            """;

        try
        {
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2714 or 1913 or 2705 or 2627)
        {
            // Idempotent: object/index already exists (concurrent create).
        }
    }
}
