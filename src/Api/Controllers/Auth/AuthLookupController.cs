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
    /// Use returned tenantId as X-Tenant-Id when calling POST /api/auth/ezofis/login.
    /// </summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(TenantLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTenantsByEmail([FromQuery] string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "Email is required." });

        try
        {
            await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
            var items = await context.UserTenants
                .AsNoTracking()
                .Where(ut => ut.Email == email.Trim())
                .Join(
                    context.Tenants.Where(t => t.IsActive),
                    ut => ut.TenantId,
                    t => t.Id,
                    (ut, t) => new TenantLookupItem(t.Id, t.Name, ut.Role))
                .ToListAsync(cancellationToken);

            return Ok(new TenantLookupResponse(items));
        }
        catch (SqlException ex) when (ex.Number == SqlErrorInvalidObjectName)
        {
            return Ok(new TenantLookupResponse(Array.Empty<TenantLookupItem>()));
        }
    }
}

/// <summary>Organizations the email belongs to.</summary>
public record TenantLookupResponse(IReadOnlyList<TenantLookupItem> Tenants);

/// <summary>Organization with tenantId for X-Tenant-Id header.</summary>
public record TenantLookupItem(Guid TenantId, string Name, string Role);
