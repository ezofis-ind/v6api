namespace SaaSApp.Repository.Application.Contracts;

public sealed record CreateRepositoryItemShareRequest(
    string Email,
    string? Message = null,
    /// <summary>When true, creates a guest user in the tenant (no password) — used for workflow inbox shares.</summary>
    bool ProvisionGuestUser = false,
    Guid? WorkflowInstanceId = null);

public sealed record CreateWorkflowInboxShareRequest(
    string Email,
    Guid RepositoryId,
    Guid ItemId,
    string? Message = null);

public sealed record CreateRepositoryItemShareResult(
    Guid ShareId,
    string ShareToken,
    Guid SourceRepositoryId,
    Guid SourceItemId,
    string RecipientEmail,
    DateTime ExpiresAtUtc,
    string ShareUrl);

/// <summary>How a share invite recipient should authenticate.</summary>
public sealed record ShareInviteAuthInfo(
    bool UserExists,
    /// <summary>True when recipient may set a first-time EZOFIS password.</summary>
    bool RequiresPasswordSetup,
    /// <summary>When set, account is locked to this provider only (<c>google</c> or <c>microsoft</c>).</summary>
    string? RequiredSocialProvider,
    /// <summary>
    /// What the sign-in page may offer: <c>password_setup</c>, <c>google</c>, <c>microsoft</c>, <c>password_login</c>.
    /// For new share guests (auth not chosen yet), all three first-time options are returned.
    /// </summary>
    IReadOnlyList<string> AllowedAuthMethods,
    string? LoginType);

public sealed record RepositoryItemSharePreviewDto(
    string ShareToken,
    Guid SourceTenantId,
    Guid SourceRepositoryId,
    Guid SourceItemId,
    string? FileName,
    string? SourceOrganizationName,
    string RecipientEmail,
    DateTime ExpiresAtUtc,
    bool RequiresLogin,
    bool RequiresPasswordSetup,
    /// <summary>When set, account already uses this provider only.</summary>
    string? RequiredSocialProvider,
    /// <summary>Options to show on sign-in. New guests get password_setup + google + microsoft.</summary>
    IReadOnlyList<string> AllowedAuthMethods,
    string? LoginType,
    bool AutoProvisionGuest,
    Guid? WorkflowInstanceId);

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

    /// <summary>Workflow inbox share: provisions guest user in tenant and creates read-only file share.</summary>
    Task<CreateRepositoryItemShareResult> CreateWorkflowInboxShareAsync(
        Guid sourceTenantId,
        Guid workflowInstanceId,
        Guid repositoryId,
        Guid itemId,
        Guid sharedByUserId,
        CreateWorkflowInboxShareRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the share recipient still needs to set their first password.</summary>
    Task<bool> RecipientRequiresPasswordSetupAsync(
        string shareToken,
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
