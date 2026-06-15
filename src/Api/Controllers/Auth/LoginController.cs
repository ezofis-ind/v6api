using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Api.Services;
using SaaSApp.MultiTenancy;
using Microsoft.Extensions.Hosting;

namespace SaaSApp.Api.Controllers.Auth;

/// <summary>Ezofis email/password login. Send X-Tenant-Id header to select organization.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class LoginController : ControllerBase
{
    private readonly IEzofisAuthService _authService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<LoginController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public LoginController(
        IEzofisAuthService authService,
        ITenantProvider tenantProvider,
        ILogger<LoginController> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _authService = authService;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>Login with email and password. If 2FA enabled, returns tempToken; call POST /2fa/complete with code.</summary>
    [HttpPost("ezofis/login")]
    [ProducesResponseType(typeof(LoginSuccess), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginRequiresTwoFactor), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null)
            return BadRequest(new { error = "X-Tenant-Id header is required to select organization." });

        try
        {
            var result = await _authService.LoginAsync(request.Email, request.Password, tenantId.Value, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Login failed due to configuration or user profile data.");
            return StatusCode(StatusCodes.Status500InternalServerError, BuildLoginConfigError(ex));
        }
    }

    /// <summary>Complete login after 2FA. Use tempToken from login response and TOTP code from authenticator app.</summary>
    [HttpPost("2fa/complete")]
    [ProducesResponseType(typeof(LoginSuccess), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CompleteTwoFactor([FromBody] CompleteTwoFactorRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.CompleteTwoFactorAsync(request.TempToken, request.Code, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "2FA completion failed due to configuration or user profile data.");
            return StatusCode(StatusCodes.Status500InternalServerError, BuildLoginConfigError(ex));
        }
    }

    private object BuildLoginConfigError(InvalidOperationException ex)
    {
        var showDetails = _environment.IsDevelopment()
            || _configuration.GetValue<bool>("Diagnostics:ShowDetailedErrors");
        if (showDetails)
            return new { error = ex.Message };
        return new { error = "Login configuration is invalid. Please contact support." };
    }
}

/// <summary>Email and password for Ezofis login. Requires X-Tenant-Id header.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>TempToken from login response when 2FA required; Code from authenticator app. Requires X-Tenant-Id header.</summary>
public record CompleteTwoFactorRequest(string TempToken, string Code);
