using Microsoft.Extensions.DependencyInjection;

namespace SaaSApp.Repository.Application;

public static class RepositoryApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddRepositoryApplication(this IServiceCollection services)
    {
        return services;
    }
}
