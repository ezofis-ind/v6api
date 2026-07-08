using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaaSApp.Billing.Application.Contracts;
using SaaSApp.Billing.Infrastructure.Services;

namespace SaaSApp.Billing.Infrastructure;

public static class BillingInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBillingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICreditService, CreditService>();
        return services;
    }
}
