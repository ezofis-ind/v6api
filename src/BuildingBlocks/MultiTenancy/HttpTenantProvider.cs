using Microsoft.AspNetCore.Http;

namespace SaaSApp.MultiTenancy;

/// <summary>
/// Extracts TenantId from X-Tenant-Id header (when user selects organization) or JWT claim (e.g. "tid").
/// Client: after login, call GET /api/me/tenants; if multiple, send selected tenant in X-Tenant-Id header.
/// </summary>
public sealed class HttpTenantProvider : ITenantProvider
{
    public const string TenantIdHeaderName = "X-Tenant-Id";
    /// <summary>Set by <c>EmailTenantResolutionMiddleware</c> when X-Tenant-Id is an email and a row exists in catalog.UserTenants.</summary>
    public const string ResolvedTenantIdFromEmailItemKey = "SaaSApp.ResolvedTenantIdFromEmail";
    public const string TenantIdClaimType = "tid"; // Entra ID tenant claim; use custom claim if needed

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetTenantId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
            return null;

        // Email in X-Tenant-Id → catalog.UserTenants (resolved by middleware before connection middleware)
        if (context.Items.TryGetValue(ResolvedTenantIdFromEmailItemKey, out var resolvedObj) && resolvedObj is Guid fromEmailResolution)
            return fromEmailResolution;

        // Prefer header so client can choose organization after login
        var headerValue = context.Request.Headers[TenantIdHeaderName].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerValue) && Guid.TryParse(headerValue, out var fromHeader))
            return fromHeader;

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var tidClaim = user.FindFirst(TenantIdClaimType)
            ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid");

        if (tidClaim == null || !Guid.TryParse(tidClaim.Value, out var tenantId))
            return null;

        return tenantId;
    }
}
