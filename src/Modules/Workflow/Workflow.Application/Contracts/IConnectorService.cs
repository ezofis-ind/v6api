using SaaSApp.Workflow.Application.Connectors;

namespace SaaSApp.Workflow.Application.Contracts;

public interface IConnectorService
{
    Task<ConnectorDto> CreateAsync(ConnectorUpsertRequest request, CancellationToken cancellationToken = default);

    Task<ConnectorDto?> UpdateAsync(Guid id, ConnectorUpsertRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectorDto>> ListAsync(ConnectorListRequest request, CancellationToken cancellationToken = default);

    Task<ConnectorDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
