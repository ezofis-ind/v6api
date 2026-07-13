using SaaSApp.ActivityLog.Application.Contracts;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Ensures activity log schema exists in tenant DB before API calls that may write access logs.
/// Applies once per tenant (cached).
/// </summary>
public sealed class ActivityLogSchemaEnsuringMiddleware
{
    private readonly RequestDelegate _next;

    public ActivityLogSchemaEnsuringMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantConnectionProvider connectionProvider,
        IActivityLogSchemaService schemaService)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var tenantId = tenantProvider.GetTenantId();
        var conn = connectionProvider.ConnectionString;
        if (tenantId == null || string.IsNullOrEmpty(conn))
        {
            await _next(context);
            return;
        }

        await TenantSchemaEnsureHelper.EnsureActivityLogSchemaAsync(
            tenantId.Value,
            conn,
            () => schemaService.ApplyBaseSchemaAsync(conn, context.RequestAborted),
            context.RequestAborted);

        await _next(context);
    }
}
