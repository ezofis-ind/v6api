using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SaaSApp.Users.Application.Behaviors;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application;

public static class UsersApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddUsersApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        services.AddScoped<Contracts.ITenantContext, TenantContext>();
        services.AddScoped<IPermissionValidator, Roles.PermissionValidator>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
