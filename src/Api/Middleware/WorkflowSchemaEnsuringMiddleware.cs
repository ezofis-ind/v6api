using SaaSApp.Api.Services;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Ensures workflow schema exists in tenant DB before workflow operations.
/// Applies schema on first workflow request per tenant (cached). Runs after TenantConnectionMiddleware.
/// </summary>
public sealed class WorkflowSchemaEnsuringMiddleware
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> AppliedTenants = new();
    private readonly RequestDelegate _next;

    public WorkflowSchemaEnsuringMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantConnectionProvider connectionProvider,
        IWorkflowSchemaService schemaService)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.Contains("/Workflows", StringComparison.OrdinalIgnoreCase))
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

        if (!AppliedTenants.ContainsKey(tenantId.Value))
        {
            await TenantSchemaEnsureHelper.EnsureWorkflowSchemaAsync(
                tenantId.Value,
                conn,
                () => schemaService.ApplySchemaAsync(conn, context.RequestAborted),
                context.RequestAborted);
            AppliedTenants.TryAdd(tenantId.Value, 0);
        }

        await _next(context);
    }
}
