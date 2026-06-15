using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Infrastructure.Persistence;

namespace SaaSApp.Api.Services;

/// <summary>Ezofis email/password login with BCrypt and optional 2FA. Issues JWT when successful.</summary>
public sealed class EzofisAuthService : IEzofisAuthService
{
    private const string PendingLoginCacheKeyPrefix = "2fa_login_";
    private static readonly TimeSpan PendingLoginExpiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AccessTokenExpiry = TimeSpan.FromDays(1);

    private readonly IUserRepository _userRepository;
    private readonly ITwoFactorService _twoFactorService;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public EzofisAuthService(
        IUserRepository userRepository,
        ITwoFactorService twoFactorService,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _twoFactorService = twoFactorService;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Email and password are required.");

        var user = await _userRepository.GetByEmailAsync(email.Trim(), cancellationToken);
        if (user == null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.TenantId != tenantId)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("Password not set. Use set-password or contact admin.");

        try
        {
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid email or password.");
        }
        catch (SaltParseException)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.TwoFactorAuthentication)
        {
            var tempToken = Guid.NewGuid().ToString("N");
            _cache.Set(PendingLoginCacheKeyPrefix + tempToken, (user.Id.ToString(), tenantId), PendingLoginExpiry);
            return new LoginRequiresTwoFactor(tempToken, tenantId, user.Id, (int)PendingLoginExpiry.TotalSeconds);
        }

        var safeEmail = user.Email?.Trim();
        if (string.IsNullOrWhiteSpace(safeEmail))
            throw new InvalidOperationException("User email is missing.");

        var safeDisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? safeEmail : user.DisplayName;
        var safeRole = string.IsNullOrWhiteSpace(user.Role) ? "User" : user.Role;

        return new LoginSuccess(
            user.Id,
            GenerateJwt(user.Id, safeEmail, safeDisplayName, safeRole, tenantId),
            "Bearer",
            (int)AccessTokenExpiry.TotalSeconds);
    }

    public async Task<LoginResult> CompleteTwoFactorAsync(string tempToken, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tempToken) || string.IsNullOrEmpty(code))
            throw new ArgumentException("TempToken and code are required.");

        var cached = _cache.Get<(string UserId, Guid TenantId)>(PendingLoginCacheKeyPrefix + tempToken);
        if (cached == default)
            throw new UnauthorizedAccessException("Session expired. Please login again.");

        if (!await _twoFactorService.VerifyAsync(cached.UserId, code, cancellationToken))
            throw new UnauthorizedAccessException("Invalid verification code.");

        _cache.Remove(PendingLoginCacheKeyPrefix + tempToken);

        var user = await _userRepository.GetByIdAsync(Guid.Parse(cached.UserId), cancellationToken);
        if (user == null)
            throw new UnauthorizedAccessException("User not found.");

        var safeEmail = user.Email?.Trim();
        if (string.IsNullOrWhiteSpace(safeEmail))
            throw new InvalidOperationException("User email is missing.");

        var safeDisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? safeEmail : user.DisplayName;
        var safeRole = string.IsNullOrWhiteSpace(user.Role) ? "User" : user.Role;

        return new LoginSuccess(
            user.Id,
            GenerateJwt(user.Id, safeEmail, safeDisplayName, safeRole, cached.TenantId),
            "Bearer",
            (int)AccessTokenExpiry.TotalSeconds);
    }

    private string GenerateJwt(Guid userId, string email, string displayName, string role, Guid tenantId)
    {
        var key = _configuration["EzofisAuth:SigningKey"];
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("EzofisAuth:SigningKey not configured.");

        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("EzofisAuth:SigningKey must be at least 32 characters.");

        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var issuer = _configuration["EzofisAuth:Issuer"] ?? "Ezofis";
        var audience = _configuration["EzofisAuth:Audience"] ?? "Ezofis";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("name", displayName),
            new Claim("role", role),
            new Claim("tid", tenantId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.Add(AccessTokenExpiry),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
