using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OtpNet;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Entities;
using SaaSApp.Users.Infrastructure.Persistence;

namespace SaaSApp.Api.Services;

/// <summary>Two-factor TOTP implementation: setup (QR), enable, disable, verify.</summary>
public sealed class TwoFactorService : ITwoFactorService
{
    private const string SetupCacheKeyPrefix = "2fa_setup_";
    private static readonly TimeSpan SetupCacheExpiry = TimeSpan.FromMinutes(5);

    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly ILogger<TwoFactorService> _logger;

    public TwoFactorService(
        IUserRepository userRepository,
        ITenantContext tenantContext,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache,
        IDataProtectionProvider dataProtection,
        ILogger<TwoFactorService> logger)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _dataProtection = dataProtection;
        _logger = logger;
    }

    public async Task<TotpSetupResult> SetupAsync(CancellationToken cancellationToken = default)
    {
        var (userId, _) = await GetCurrentUserAsync(cancellationToken);
        if (userId == null)
            throw new UnauthorizedAccessException("User not authenticated.");

        var secret = new byte[20];
        RandomNumberGenerator.Fill(secret);
        var base32Secret = Base32Encoding.ToString(secret).Replace("=", "");

        var totp = new Totp(secret, step: 30);
        var issuer = "Ezofis";
        var accountName = $"Ezofis ({userId})";
        var qrUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";

        _cache.Set(SetupCacheKeyPrefix + userId, base32Secret, SetupCacheExpiry);

        return new TotpSetupResult(qrUri, base32Secret);
    }

    public async Task<bool> EnableAsync(string code, CancellationToken cancellationToken = default)
    {
        var (userId, user) = await GetCurrentUserAsync(cancellationToken);
        if (userId == null || user == null)
            throw new UnauthorizedAccessException("User not authenticated.");

        var pendingSecret = _cache.Get<string>(SetupCacheKeyPrefix + userId);
        if (string.IsNullOrEmpty(pendingSecret))
            throw new InvalidOperationException("2FA setup expired or not started. Call POST /api/auth/2fa/setup first.");

        var secretBytes = Base32Encoding.ToBytes(pendingSecret);
        var totp = new Totp(secretBytes, step: 30);
        if (!totp.VerifyTotp(code.Trim().Replace(" ", ""), out _, new VerificationWindow(1, 1)))
        {
            _logger.LogWarning("Invalid 2FA code during enable for user {UserId}", userId);
            return false;
        }

        var protector = _dataProtection.CreateProtector("Ezofis.Totp");
        var encrypted = protector.Protect(Convert.ToBase64String(secretBytes));
        user.EnableTwoFactor(encrypted);
        _userRepository.Update(user);

        _cache.Remove(SetupCacheKeyPrefix + userId);
        _logger.LogInformation("2FA enabled for user {UserId}", userId);
        return true;
    }

    public async Task<bool> DisableAsync(string code, CancellationToken cancellationToken = default)
    {
        var (_, user) = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
            throw new UnauthorizedAccessException("User not authenticated.");

        if (!user.TwoFactorAuthentication)
            return true;

        if (!await VerifyAsync(user.Id.ToString(), code, cancellationToken))
        {
            _logger.LogWarning("Invalid 2FA code during disable for user {UserId}", user.Id);
            return false;
        }

        user.DisableTwoFactor();
        _userRepository.Update(user);
        _logger.LogInformation("2FA disabled for user {UserId}", user.Id);
        return true;
    }

    public async Task<bool> VerifyAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            return false;

        var user = await _userRepository.GetByIdAsync(Guid.Parse(userId), cancellationToken);
        if (user == null || string.IsNullOrEmpty(user.TotpSecretEncrypted))
            return false;

        var protector = _dataProtection.CreateProtector("Ezofis.Totp");
        var decrypted = protector.Unprotect(user.TotpSecretEncrypted);
        var secretBytes = Convert.FromBase64String(decrypted);
        var totp = new Totp(secretBytes, step: 30);
        return totp.VerifyTotp(code.Trim().Replace(" ", ""), out _, new VerificationWindow(1, 1));
    }

    private async Task<(string? UserId, User? User)> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null)
            return (null, null);

        var httpContext = _httpContextAccessor.HttpContext;
        var email = httpContext?.User?.FindFirst("email")?.Value
            ?? httpContext?.User?.FindFirst("preferred_username")?.Value
            ?? httpContext?.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
            ?? httpContext?.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(email))
            return (null, null);

        var user = await _userRepository.GetByEmailAsync(email.Trim(), cancellationToken);
        if (user == null)
            return (null, null);

        return (user.Id.ToString(), user);
    }
}
