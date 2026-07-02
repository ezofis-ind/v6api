namespace SaaSApp.Repository.Application.Contracts;

/// <summary>Validated share grant — use source tenant + repo/item ids with existing repository APIs.</summary>
public sealed record RepositoryShareAccess(
    Guid SourceTenantId,
    Guid SourceRepositoryId,
    Guid SourceItemId,
    string ShareToken,
    bool ReadOnly = true);
