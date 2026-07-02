using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SaaSApp.Catalog.Persistence;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// When <see cref="HttpTenantProvider.TenantIdHeaderName"/> is not a GUID but looks like an email,
/// resolves the tenant from <c>catalog.UserTenants</c> (same idea as org picker / legacy flow).
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
        var cacheKey = "email-tenant:" + email;
        if (cache.TryGetValue(cacheKey, out Guid cachedTenantId) && cachedTenantId != Guid.Empty)
        {
            context.Items[HttpTenantProvider.ResolvedTenantIdFromEmailItemKey] = cachedTenantId;
            await _next(context);
            return;
        }

        await using var catalog = await catalogFactory.CreateDbContextAsync(context.RequestAborted);
        var tenantId = await catalog.UserTenants
            .AsNoTracking()
            .Where(ut => ut.Email == email)
            .OrderBy(ut => ut.TenantId)
            .Select(ut => ut.TenantId)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (tenantId != Guid.Empty)
        {
            cache.Set(cacheKey, tenantId, EmailTenantCacheTtl);
            context.Items[HttpTenantProvider.ResolvedTenantIdFromEmailItemKey] = tenantId;
        }

        await _next(context);
    }
}
