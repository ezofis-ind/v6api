using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SaaSApp.Catalog.Persistence;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// When <see cref="HttpTenantProvider.TenantIdHeaderName"/> is not a GUID but looks like an email,
/// resolves the tenant from <c>catalog.UserTenants</c> (same idea as org picker / legacy flow).
/// If the email belongs to multiple tenants, returns 409 so the client must send a tenant GUID.
/// Must run before <see cref="TenantConnectionMiddleware"/>.
/// </summary>
public sealed class EmailTenantResolutionMiddleware
{
    private static readonly TimeSpan EmailTenantCacheTtl = TimeSpan.FromMinutes(15);
    private readonly RequestDelegate _next;

    public EmailTenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IDbContextFactory<CatalogDbContext> catalogFactory,
        IMemoryCache cache)
    {
        // Before signup there is no tenant id; do not interpret X-Tenant-Id (email or guid) for these routes.
        if (TenantSignupOtpPathHelper.Matches(context))
        {
            await _next(context);
            return;
        }

        var header = context.Request.Headers[HttpTenantProvider.TenantIdHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
        {
            await _next(context);
            return;
        }

        header = header.Trim();
        if (Guid.TryParse(header, out _))
        {
            await _next(context);
            return;
        }

        if (header.IndexOf('@', StringComparison.Ordinal) < 0)
        {
            await _next(context);
            return;
        }

        var email = header.ToLowerInvariant();
        // v2: only cache unambiguous (single-tenant) resolutions; avoids stale FirstOrDefault picks.
        var cacheKey = "email-tenant-v2:" + email;
        if (cache.TryGetValue(cacheKey, out Guid cachedTenantId) && cachedTenantId != Guid.Empty)
        {
            context.Items[HttpTenantProvider.ResolvedTenantIdFromEmailItemKey] = cachedTenantId;
            await _next(context);
            return;
        }

        await using var catalog = await catalogFactory.CreateDbContextAsync(context.RequestAborted);
        var matches = await (
                from ut in catalog.UserTenants.AsNoTracking()
                join t in catalog.Tenants.AsNoTracking() on ut.TenantId equals t.Id
                where t.IsActive && ut.Email.ToLower() == email
                orderby t.Name
                select new { t.Id, t.Name, ut.Role })
            .ToListAsync(context.RequestAborted);

        if (matches.Count == 0)
        {
            await _next(context);
            return;
        }

        if (matches.Count > 1)
        {
            // Ambiguous: do not pick a random tenant. Client must call GET /api/auth/tenants and send GUID.
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new
                {
                    error = "Multiple tenants found for this email. Call GET /api/auth/tenants?email=... , let the user pick an organization, then send X-Tenant-Id as that tenant GUID (not the email).",
                    requiresOrgSelection = true,
                    tenants = matches.Select(m => new { tenantId = m.Id, name = m.Name, role = m.Role }).ToList()
                }),
                context.RequestAborted);
            return;
        }

        var tenantId = matches[0].Id;
        cache.Set(cacheKey, tenantId, EmailTenantCacheTtl);
        context.Items[HttpTenantProvider.ResolvedTenantIdFromEmailItemKey] = tenantId;

        await _next(context);
    }
}
