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
}
