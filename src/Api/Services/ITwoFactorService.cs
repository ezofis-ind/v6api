namespace SaaSApp.Api.Services;

/// <summary>Two-factor authentication (TOTP) setup, enable, disable, and verification.</summary>
public interface ITwoFactorService
{
    /// <summary>Start 2FA setup. Generates TOTP secret and returns QR URI. User must call EnableAsync with a code within 5 minutes.</summary>
    Task<TotpSetupResult> SetupAsync(CancellationToken cancellationToken = default);

    /// <summary>Enable 2FA. Verifies the code from authenticator app and persists the secret.</summary>
    Task<bool> EnableAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Disable 2FA. Requires current TOTP code to confirm.</summary>
    Task<bool> DisableAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Verify TOTP code for a user. Used during login when 2FA is enabled.</summary>
    Task<bool> VerifyAsync(string userId, string code, CancellationToken cancellationToken = default);
}

/// <summary>QR code URI for authenticator app and manual entry key (base32).</summary>
public record TotpSetupResult(string QrCodeUri, string ManualEntryKey);
