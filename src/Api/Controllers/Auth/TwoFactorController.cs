using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Api.Services;
using SaaSApp.Security;

namespace SaaSApp.Api.Controllers.Auth;

/// <summary>2FA setup, enable, and disable. User must be authenticated (Azure AD, Auth0, or Ezofis JWT) and send X-Tenant-Id.</summary>
[ApiController]
[Route("api/auth/2fa")]
[Authorize(Policy = AuthorizationPolicies.AnyAuthenticated)]
public sealed class TwoFactorController : ControllerBase
{
    private readonly ITwoFactorService _twoFactorService;

    public TwoFactorController(ITwoFactorService twoFactorService)
    {
        _twoFactorService = twoFactorService;
    }

    /// <summary>Start 2FA setup. Returns QR code URI and manual entry key. Scan QR with authenticator app, then call POST /enable with a code.</summary>
    [HttpPost("setup")]
    [ProducesResponseType(typeof(TotpSetupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        var result = await _twoFactorService.SetupAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Enable 2FA. Verify with a code from your authenticator app (from setup).</summary>
    [HttpPost("enable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Enable([FromBody] EnableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code is required." });

        var success = await _twoFactorService.EnableAsync(request.Code, cancellationToken);
        if (!success)
            return BadRequest(new { error = "Invalid verification code." });

        return Ok(new { enabled = true });
    }

    /// <summary>Disable 2FA. Requires current TOTP code to confirm.</summary>
    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable([FromBody] DisableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code is required." });

        var success = await _twoFactorService.DisableAsync(request.Code, cancellationToken);
        if (!success)
            return BadRequest(new { error = "Invalid verification code." });

        return Ok(new { disabled = true });
    }
}

/// <summary>TOTP code from authenticator app (6 digits).</summary>
public record EnableTwoFactorRequest(string Code);

/// <summary>Current TOTP code to confirm identity before disabling 2FA.</summary>
public record DisableTwoFactorRequest(string Code);
