using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Repository.Infrastructure.Options;
using SaaSApp.Repository.Infrastructure.Services;
using SaaSApp.Repository.Infrastructure.Storage;

namespace SaaSApp.Repository.Infrastructure;

public static class RepositoryInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRepositoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RepositoryFileStorageOptions>(configuration.GetSection(RepositoryFileStorageOptions.SectionName));
        services.AddScoped<IRepositorySchemaService, RepositorySchemaService>();
        services.AddScoped<IRepositoryStorageSeedService, RepositoryStorageSeedService>();
        services.AddScoped<IStaticRepositoryProvisioner, StaticRepositoryProvisioner>();
        services.AddScoped<IRepositoryBrowseService, RepositoryBrowseService>();
        services.AddScoped<IRepositoryFolderService, RepositoryFolderService>();
        services.AddScoped<IRepositoryItemQueryService, RepositoryItemQueryService>();
        services.AddScoped<IRepositoryItemActivityService, RepositoryItemActivityService>();
        services.AddScoped<LocalRepositoryFileStorage>();
        services.AddScoped<EzofisBlobRepositoryFileStorage>();
        services.AddScoped<IRepositoryFileStorage, RepositoryFileStorageRouter>();
        services.AddScoped<RepositoryWorkflowAttachService>();
        services.AddScoped<IRepositoryFileUploadService, RepositoryFileUploadService>();
        services.AddScoped<IRepositoryArchiveFileUploadService, RepositoryArchiveFileUploadService>();
        return services;
    }
}
