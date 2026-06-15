using Azure.Storage.Blobs;

namespace SaaSApp.BlobStorage;

public interface IEzofisBlobStorageService
{
    bool IsConfigured { get; }

    BlobContainerClient GetTenantContainer(Guid tenantId, bool createIfNotExists = true);

    BlobClient GetBlobClient(Guid tenantId, string blobPath, bool createContainerIfNotExists = true);
}
