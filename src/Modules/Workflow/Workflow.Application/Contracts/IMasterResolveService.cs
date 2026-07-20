using SaaSApp.Workflow.Application.Connectors;

namespace SaaSApp.Workflow.Application.Contracts;

public interface IMasterResolveService
{
    Task<MasterResolveResponse> ResolveAsync(
        string type,
        string? q,
        int maxResults,
        string? source,
        string? formId,
        Guid? connectorId,
        Guid? mailboxId,
        CancellationToken cancellationToken = default);
}
