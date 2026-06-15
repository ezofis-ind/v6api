using Microsoft.Extensions.DependencyInjection;

namespace SaaSApp.MultiTenancy;

public static class MultiTenancyServiceCollectionExtensions
{
public static IServiceCollection AddMultiTenancy(this IServiceCollection services)
{
    services.AddHttpContextAccessor();
    services.AddScoped<JobExecutionContext>();
    services.AddScoped<HttpTenantProvider>();
    services.AddScoped<ITenantProvider, AmbientTenantProvider>();
    services.AddScoped<ITenantConnectionProvider, TenantConnectionProvider>();
    return services;
}
}
