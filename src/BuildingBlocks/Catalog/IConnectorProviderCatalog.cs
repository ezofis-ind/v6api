using SaaSApp.Catalog.Entities;

namespace SaaSApp.Catalog;

public interface IConnectorProviderCatalog
{
    Task EnsureSchemaAndSeedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectorProvider>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<ConnectorProvider?> GetByCodeAsync(string providerCode, CancellationToken cancellationToken = default);

    Task UpsertCredentialsAsync(
        string providerCode,
        string clientId,
        string clientSecret,
        string redirectUri,
        string? scopes = null,
        CancellationToken cancellationToken = default);
}
