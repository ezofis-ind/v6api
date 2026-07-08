using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace SaaSApp.Billing.Application;

public static class BillingApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBillingApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        return services;
    }
}
