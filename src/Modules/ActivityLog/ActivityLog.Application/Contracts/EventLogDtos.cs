namespace SaaSApp.ActivityLog.Application.Contracts;

public sealed record EventLogEntry(
    Guid Id,
    Guid TenantId,
    Guid? UserId,
    string? UserDisplayName,
    string? UserEmail,
    string EventTitle,
    string EventType,
    string Category,
    string Severity,
    string? IpAddress,
    string? HttpMethod,
    string? Path,
    int? StatusCode,
    string? CorrelationId,
    DateTime CreatedAtUtc);

public sealed record EventLogEntryDto(
    Guid Id,
    string EventTitle,
    string EventType,
    string? UserDisplayName,
    string? UserEmail,
    string Category,
    string Severity,
    string? IpAddress,
    DateTime CreatedAtUtc);

public sealed record ListEventLogsQuery(
    int Page = 1,
    int PageSize = 50,
    string? Category = null,
    string? Severity = null,
    string? UserEmail = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Search = null);

public interface IEventLogWriter
{
    void Enqueue(EventLogEntry entry, string connectionString);
}

public interface IEventLogQueryService
{
    Task<PagedResult<EventLogEntryDto>> ListAsync(
        Guid tenantId,
        ListEventLogsQuery query,
        CancellationToken cancellationToken = default);
}
