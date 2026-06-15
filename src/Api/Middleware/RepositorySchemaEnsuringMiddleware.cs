using SaaSApp.Repository.Application.Contracts;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Ensures repository schema exists in tenant DB before repository API calls.
/// Applies once per tenant (cached). Required for tenants created before repository module was added.
/// </summary>
public sealed class RepositorySchemaEnsuringMiddleware
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> AppliedTenants = new();
    private readonly RequestDelegate _next;

    public RepositorySchemaEnsuringMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantConnectionProvider connectionProvider,
        IRepositorySchemaService schemaService)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/repositories", StringComparison.OrdinalIgnoreCase))
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
            await schemaService.ApplyBaseSchemaAsync(conn, context.RequestAborted);
            AppliedTenants.TryAdd(tenantId.Value, 0);
        }

        await _next(context);
    }
}
