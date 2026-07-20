using SaaSApp.Workflow.Application.Workflows;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Application.Contracts;

public interface IWorkflowEmailIngestLinker
{
    /// <summary>
    /// Sync EmailIngestMailbox for EMAIL-initiated workflows; disable when mode is no longer EMAIL.
    /// May update stored workflow JSON so MailInitiate.ConnectorId is the OAuth Guid.
    /// </summary>
    Task<WorkflowEmailIngestLinkResult> SyncAsync(
        Guid workflowId,
        WorkflowJsonDto? workflowJson,
        string? workflowJsonRaw,
        WorkflowEmailIngestOptions? options,
        CancellationToken cancellationToken = default);
}
