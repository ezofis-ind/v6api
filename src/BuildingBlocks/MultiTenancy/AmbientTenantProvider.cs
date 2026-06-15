namespace SaaSApp.MultiTenancy;

/// <summary>Prefers <see cref="JobExecutionContext"/> when a background job is running; otherwise HTTP/JWT.</summary>
public sealed class AmbientTenantProvider : ITenantProvider
{
    private readonly JobExecutionContext _jobContext;
    private readonly HttpTenantProvider _httpTenantProvider;

    public AmbientTenantProvider(JobExecutionContext jobContext, HttpTenantProvider httpTenantProvider)
    {
        _jobContext = jobContext;
        _httpTenantProvider = httpTenantProvider;
    }

    public Guid? GetTenantId() =>
        _jobContext.IsActive ? _jobContext.TenantId : _httpTenantProvider.GetTenantId();
}
