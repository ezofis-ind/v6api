using Microsoft.Extensions.DependencyInjection;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Workflow.Application;

public static class WorkflowApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(WorkflowApplicationServiceCollectionExtensions).Assembly));
        // Note: ITenantContext is registered in Infrastructure layer (WorkflowTenantContext)
        // because it needs access to ITenantConnectionProvider
        return services;
    }
}
