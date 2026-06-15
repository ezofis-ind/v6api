namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Persists designer form JSON (v5 Form Json/{id}.json or blob).</summary>
public interface IFormJsonStorageService
{
    Task SaveFormJsonAsync(string formId, string json, CancellationToken cancellationToken = default);
    Task<string?> GetFormJsonAsync(string formId, CancellationToken cancellationToken = default);
}
