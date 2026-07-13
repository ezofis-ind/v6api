namespace SaaSApp.ActivityLog.Application.Contracts;

public sealed record ActivityLogEntry(
    Guid Id,
    Guid TenantId,
    Guid? UserId,
    string? UserEmail,
    string HttpMethod,
    string Path,
    string? QueryString,
    int StatusCode,
    int DurationMs,
    string? CorrelationId,
    string? ClientIp,
    string? UserAgent,
    DateTime CreatedAtUtc);

public sealed record ActivityLogEntryDto(
    Guid Id,
    Guid TenantId,
    Guid? UserId,
    string? UserEmail,
    string HttpMethod,
    string Path,
    string? QueryString,
    int StatusCode,
    int DurationMs,
    string? CorrelationId,
    string? ClientIp,
    string? UserAgent,
    DateTime CreatedAtUtc);

public sealed record ListActivityLogsQuery(
    int Page = 1,
    int PageSize = 50,
    Guid? UserId = null,
    string? Method = null,
    string? Path = null,
    int? StatusCode = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? CorrelationId = null);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Data,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
