namespace SaaSApp.ActivityLog.Application.Contracts;

public interface IActivityLogQueryService
{
    Task<PagedResult<ActivityLogEntryDto>> ListAsync(
        Guid tenantId,
        ListActivityLogsQuery query,
        CancellationToken cancellationToken = default);
}
