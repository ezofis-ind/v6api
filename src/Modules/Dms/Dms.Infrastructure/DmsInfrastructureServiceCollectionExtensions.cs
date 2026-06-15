using Microsoft.Extensions.DependencyInjection;
using SaaSApp.Dms.Infrastructure.Services;

namespace SaaSApp.Dms.Infrastructure;

public static class DmsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDmsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDmsFolderService, DmsFolderService>();
        return services;
    }
}
