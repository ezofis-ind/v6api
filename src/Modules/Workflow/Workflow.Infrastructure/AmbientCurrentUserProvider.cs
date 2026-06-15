using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure;

/// <summary>Prefers <see cref="JobExecutionContext"/> when a background job is running; otherwise JWT.</summary>
public sealed class AmbientCurrentUserProvider : ICurrentUserProvider
{
    private readonly JobExecutionContext _jobContext;
    private readonly CurrentUserProvider _httpUserProvider;

    public AmbientCurrentUserProvider(JobExecutionContext jobContext, CurrentUserProvider httpUserProvider)
    {
        _jobContext = jobContext;
        _httpUserProvider = httpUserProvider;
    }

    public Guid? GetUserId() =>
        _jobContext.IsActive && _jobContext.UserId.HasValue
            ? _jobContext.UserId
            : _httpUserProvider.GetUserId();
}
