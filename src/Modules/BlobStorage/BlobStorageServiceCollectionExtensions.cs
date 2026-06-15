using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SaaSApp.BlobStorage;

public static class BlobStorageServiceCollectionExtensions
{
    public static IServiceCollection AddEzofisBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EzofisBlobStorageOptions>(options =>
        {
            configuration.GetSection(EzofisBlobStorageOptions.SectionName).Bind(options);

            // Legacy section name (WorkflowJsonStorage) — remove after all environments migrate.
            if (!options.IsConfigured)
            {
                var legacyConnection = configuration["WorkflowJsonStorage:Blob:ConnectionString"]
                    ?? configuration["WorkflowJsonStorage:ConnectionString"];
                if (!string.IsNullOrWhiteSpace(legacyConnection))
                {
                    options.ConnectionString = legacyConnection;
                    options.ContainerPrefix = configuration["WorkflowJsonStorage:Blob:ContainerPrefix"]
                        ?? options.ContainerPrefix;
                }
            }
        });

        services.AddSingleton<IEzofisBlobStorageService, EzofisBlobStorageService>();
        return services;
    }
}
