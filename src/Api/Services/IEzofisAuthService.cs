namespace SaaSApp.Api.Services;

/// <summary>Ezofis email/password authentication with optional 2FA.</summary>
public interface IEzofisAuthService
{
    /// <summary>Login with email and password. Returns JWT or LoginRequiresTwoFactor if 2FA enabled.</summary>
    Task<LoginResult> LoginAsync(string email, string password, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Complete login after 2FA. Verifies TOTP code and returns JWT.</summary>
    Task<LoginResult> CompleteTwoFactorAsync(string tempToken, string code, CancellationToken cancellationToken = default);
}

/// <summary>Result of Ezofis login: either success with JWT or 2FA required with temp token.</summary>
public abstract record LoginResult;

/// <summary>Login succeeded. Use AccessToken in Authorization: Bearer header.</summary>
public sealed record LoginSuccess(Guid UserId, string AccessToken, string TokenType, int ExpiresIn) : LoginResult;

/// <summary>2FA required. Call POST /api/auth/2fa/complete with TempToken and TOTP code. Send X-Tenant-Id: TenantId.</summary>
public sealed record LoginRequiresTwoFactor(string TempToken, Guid TenantId, Guid UserId, int ExpiresInSeconds) : LoginResult;
