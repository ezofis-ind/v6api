namespace SaaSApp.Repository.Application.Contracts;

/// <summary>
/// Provisions invited external users into a tenant for workflow inbox file shares.
/// Guest users are created without a password until they complete first-time setup.
/// </summary>
public interface IShareGuestUserProvisioningService
{
    /// <summary>Creates tenant user + catalog UserTenants row when missing. Idempotent.</summary>
    Task<Guid> EnsureGuestUserAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>True when the user exists in the tenant but has no password hash yet (EZOFIS only).</summary>
    Task<bool> RequiresPasswordSetupAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves whether share invite needs password setup or Google/Microsoft social login.</summary>
    Task<ShareInviteAuthInfo> GetShareInviteAuthInfoAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>Sets the first password for a guest-invited user (EZOFIS only).</summary>
    Task<bool> SetFirstPasswordAsync(
        Guid tenantId,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks a pending guest (no password yet) to Google or Microsoft on first social sign-in.
    /// </summary>
    Task<Guid> ConfirmGuestSocialLoginAsync(
        Guid tenantId,
        string email,
        string provider,
        CancellationToken cancellationToken = default);
}
