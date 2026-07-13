using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SaaSApp.Catalog.Persistence;

namespace SaaSApp.Api.Controllers.Auth;

/// <summary>Unauthenticated auth lookups. Use on login page to show org picker before password.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthLookupController : ControllerBase
{
    private const int SqlErrorInvalidObjectName = 208;

    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;

    public AuthLookupController(IDbContextFactory<CatalogDbContext> catalogFactory)
    {
        _catalogFactory = catalogFactory;
    }

    /// <summary>
    /// Returns organizations (tenants) for an email. Call this on the login page after user enters email.
    /// No auth required. If one tenant: show password field. If multiple: show org picker, then password.
    /// Use the selected tenantId as X-Tenant-Id (GUID) when calling POST /api/auth/ezofis/login.
    /// Do not treat multiple tenants as an error — the user must pick one organization.
    /// </summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(TenantLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTenantsByEmail([FromQuery] string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "Email is required." });

        var normalizedEmail = email.Trim().ToLowerInvariant();

        try
        {
            await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
            var items = await (
                    from ut in context.UserTenants.AsNoTracking()
                    join t in context.Tenants.AsNoTracking() on ut.TenantId equals t.Id
                    where t.IsActive && ut.Email.ToLower() == normalizedEmail
                    orderby t.Name
                    select new TenantLookupItem(t.Id, t.Name, ut.Role))
                .ToListAsync(cancellationToken);

            return Ok(new TenantLookupResponse(
                items,
                RequiresOrgSelection: items.Count > 1));
        }
        catch (SqlException ex) when (ex.Number == SqlErrorInvalidObjectName)
        {
            return Ok(new TenantLookupResponse(Array.Empty<TenantLookupItem>(), RequiresOrgSelection: false));
        }
    }
}

/// <summary>Organizations the email belongs to. When <see cref="RequiresOrgSelection"/> is true, show an org picker before login.</summary>
public record TenantLookupResponse(
    IReadOnlyList<TenantLookupItem> Tenants,
    bool RequiresOrgSelection = false);

/// <summary>Organization with tenantId for X-Tenant-Id header.</summary>
public record TenantLookupItem(Guid TenantId, string Name, string Role);
