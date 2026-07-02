namespace SaaSApp.Repository.Application.Contracts;

public sealed record CreateRepositoryItemShareRequest(
    string Email,
    string? Message = null);

public sealed record CreateRepositoryItemShareResult(
    Guid ShareId,
    string ShareToken,
    Guid SourceRepositoryId,
    Guid SourceItemId,
    string RecipientEmail,
    DateTime ExpiresAtUtc,
    string ShareUrl);

public sealed record RepositoryItemSharePreviewDto(
    string ShareToken,
    Guid SourceRepositoryId,
    Guid SourceItemId,
    string? FileName,
    string? SourceOrganizationName,
    string RecipientEmail,
    DateTime ExpiresAtUtc,
    bool RequiresLogin);

/// <summary>A file that was shared with the logged-in user (for the "Shared with me" list).</summary>
public sealed record SharedWithMeItemDto(
    Guid ShareId,
    string ShareToken,
    Guid SourceRepositoryId,
    Guid SourceItemId,
    string? FileName,
    string? SourceOrganizationName,
    DateTime SharedAtUtc,
    DateTime ExpiresAtUtc);

public interface IRepositoryItemShareService
{
    Task<CreateRepositoryItemShareResult> CreateShareAsync(
        Guid sourceTenantId,
        Guid repositoryId,
        Guid itemId,
        Guid sharedByUserId,
        CreateRepositoryItemShareRequest request,
        CancellationToken cancellationToken = default);

    Task<RepositoryItemSharePreviewDto?> GetPreviewAsync(
        string shareToken,
        CancellationToken cancellationToken = default);

    /// <summary>Active shares for a logged-in recipient (so they can reopen without the email link).</summary>
    Task<IReadOnlyList<SharedWithMeItemDto>> ListSharesForRecipientAsync(
        string recipientEmail,
        CancellationToken cancellationToken = default);

    /// <summary>Validate share token + viewer email for existing repository API routes.</summary>
    Task<RepositoryShareAccess?> ResolveShareAccessAsync(
        string shareToken,
        string viewerEmail,
        Guid? repositoryId = null,
        Guid? itemId = null,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeShareAsync(
        Guid shareId,
        Guid sourceTenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
