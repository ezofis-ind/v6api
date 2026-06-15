using Microsoft.Extensions.DependencyInjection;

namespace SaaSApp.Billing.Application;

public static class BillingApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBillingApplication(this IServiceCollection services)
    {
        return services;
    }
}
