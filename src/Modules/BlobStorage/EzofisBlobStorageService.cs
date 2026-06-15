using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace SaaSApp.BlobStorage;

public sealed class EzofisBlobStorageService : IEzofisBlobStorageService
{
    private readonly EzofisBlobStorageOptions _options;
    private readonly BlobServiceClient? _serviceClient;

    public EzofisBlobStorageService(IOptions<EzofisBlobStorageOptions> options)
    {
        _options = options.Value;
        if (_options.IsConfigured)
            _serviceClient = new BlobServiceClient(_options.ConnectionString!);
    }

    public bool IsConfigured => _options.IsConfigured && _serviceClient != null;

    public BlobContainerClient GetTenantContainer(Guid tenantId, bool createIfNotExists = true)
    {
        var container = ResolveContainer(tenantId);
        if (createIfNotExists)
            container.CreateIfNotExists();
        return container;
    }

    public BlobClient GetBlobClient(Guid tenantId, string blobPath, bool createContainerIfNotExists = true)
    {
        var normalized = NormalizeBlobPath(blobPath);
        var container = GetTenantContainer(tenantId, createContainerIfNotExists);
        return container.GetBlobClient(normalized);
    }

    private BlobContainerClient ResolveContainer(Guid tenantId)
    {
        if (_serviceClient == null)
            throw new InvalidOperationException("EzofisBlobStorage is not configured. Set EzofisBlobStorage:ConnectionString in appsettings.");

        var containerName = EzofisBlobStorageOptions.BuildContainerName(_options.ContainerPrefix, tenantId);
        return _serviceClient.GetBlobContainerClient(containerName);
    }

    private static string NormalizeBlobPath(string blobPath) =>
        blobPath.Replace('\\', '/').TrimStart('/');
}
