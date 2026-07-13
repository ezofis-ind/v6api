using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaaSApp.ActivityLog.Application.Contracts;
using SaaSApp.ActivityLog.Infrastructure.Options;
using SaaSApp.ActivityLog.Infrastructure.Services;

namespace SaaSApp.ActivityLog.Infrastructure;

public static class ActivityLogInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddActivityLogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ActivityLogOptions>(configuration.GetSection(ActivityLogOptions.SectionName));
        services.AddSingleton<IActivityLogWriter, ActivityLogWriter>();
        services.AddScoped<ActivityLogInsertService>();
        services.AddScoped<IActivityLogSchemaService, ActivityLogSchemaService>();
        services.AddScoped<IActivityLogQueryService, ActivityLogQueryService>();
        return services;
    }
}
