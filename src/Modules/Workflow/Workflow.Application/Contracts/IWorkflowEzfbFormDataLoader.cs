namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Loads jsonId → value JSON from dbo.ezfb_{form}_items for inbox / mailbox.</summary>
public interface IWorkflowEzfbFormDataLoader
{
    Task<string?> LoadFormDataJsonAsync(
        string formId,
        int formEntryId,
        CancellationToken cancellationToken = default);
}
