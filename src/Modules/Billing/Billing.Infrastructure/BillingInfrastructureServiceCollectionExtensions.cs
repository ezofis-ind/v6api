using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SaaSApp.Billing.Infrastructure;

public static class BillingInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBillingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
