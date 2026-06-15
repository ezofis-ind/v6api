using Microsoft.AspNetCore.Mvc;
using SaaSApp.Api.Services;

namespace SaaSApp.Api.Controllers;

/// <summary>Tenant signup. No JWT required. Creates tenant DB, catalog entry, and optionally admin user for Ezofis login.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SignupController : ControllerBase
{
    private readonly ITenantSignupService _signupService;

    public SignupController(ITenantSignupService signupService)
    {
        _signupService = signupService;
    }

    /// <summary>
    /// Sign up a new tenant: creates a new database, applies migrations, and registers in the catalog.
    /// TenantId is optional (auto-generated). Provide Name and optionally Email, OrganizationName.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenantSignupResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) && string.IsNullOrWhiteSpace(request.OrganizationName))
            return BadRequest(new { error = "Name or OrganizationName is required." });

        try
        {
            var result = await _signupService.SignupAsync(
                new TenantSignupRequest(
                    request.TenantId,
                    request.Name ?? "",
                    request.OrganizationName,
                    request.Email,
                    request.Password,
                    request.LoginType,
                    request.LicenseType,
                    request.FirstName,
                    request.LastName,
                    request.DatabaseName,
                    request.SignupSource,
                    request.Platform,
                    request.AppVersion),
                cancellationToken);
            return CreatedAtAction(nameof(Signup), new { id = result.TenantId }, result);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("already registered", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }
}

/// <summary>Signup request. No JWT required for this endpoint.</summary>
public record SignupRequest(
    Guid? TenantId = null,
    string? Name = null,
    string? OrganizationName = null,
    string? Email = null,
    string? Password = null,
    string? LoginType = null,
    int? LicenseType = null,
    string? FirstName = null,
    string? LastName = null,
    string? DatabaseName = null,
    string? SignupSource = null,
    string? Platform = null,
    string? AppVersion = null);
