using Microsoft.Extensions.Options;
using SaaSApp.ActivityLog.Application.Contracts;
using SaaSApp.ActivityLog.Infrastructure.Options;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Ensures activity log schema exists in tenant DB before API calls that may write access/event logs.
/// Applies once per tenant (cached). Skips when both ActivityLog and EventLog are disabled.
/// </summary>
public sealed class ActivityLogSchemaEnsuringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ActivityLogOptions _activityLogOptions;
    private readonly EventLogOptions _eventLogOptions;

    public ActivityLogSchemaEnsuringMiddleware(
        RequestDelegate next,
        IOptions<ActivityLogOptions> activityLogOptions,
        IOptions<EventLogOptions> eventLogOptions)
    {
        _next = next;
        _activityLogOptions = activityLogOptions.Value;
        _eventLogOptions = eventLogOptions.Value;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantConnectionProvider connectionProvider,
        IActivityLogSchemaService schemaService)
    {
        if (!_activityLogOptions.Enabled && !_eventLogOptions.Enabled)
        {
            await _next(context);
            return;
        }

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
