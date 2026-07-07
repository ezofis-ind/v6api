using SaaSApp.MultiTenancy;
using SaaSApp.Users.Infrastructure.Persistence;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Ensures users schema objects exist before permission- and role-menu-related API calls.
/// Applies once per tenant (cached). Required for tenants created before these tables were added.
/// </summary>
public sealed class UsersPermissionSchemaEnsuringMiddleware
{
    private readonly RequestDelegate _next;

    public UsersPermissionSchemaEnsuringMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantConnectionProvider connectionProvider)
    {
        var path = context.Request.Path.Value;
        var method = context.Request.Method;
        var needsPermissionCategories = RequiresPermissionCategories(method, path);
        var needsMenus = RequiresMenus(method, path);
        var needsRoleMenus = RequiresRoleMenus(method, path);

        if (!needsPermissionCategories && !needsMenus && !needsRoleMenus)
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

        if (needsPermissionCategories)
        {
            await TenantSchemaEnsureHelper.EnsurePermissionCategoriesAsync(
                tenantId.Value,
                conn,
                () => UsersSchemaEnsurer.EnsurePermissionCategoriesAsync(conn, context.RequestAborted),
                context.RequestAborted);
        }

        if (needsMenus)
        {
            await TenantSchemaEnsureHelper.EnsureMenusTablesAsync(
                tenantId.Value,
                conn,
                () => UsersSchemaEnsurer.EnsureMenusTablesAsync(conn, context.RequestAborted),
                context.RequestAborted);
        }

        if (needsRoleMenus)
        {
            await TenantSchemaEnsureHelper.EnsureRoleMenusTablesAsync(
                tenantId.Value,
                conn,
                () => UsersSchemaEnsurer.EnsureRoleMenusTablesAsync(conn, context.RequestAborted),
                context.RequestAborted);
        }

        await _next(context);
    }

    private static bool RequiresPermissionCategories(string method, string? path)
    {
        if (!HttpMethods.IsGet(method) || string.IsNullOrEmpty(path))
            return false;

        if (path.Equals("/api/usersession", StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.Equals("/api/users/roles/permissions", StringComparison.OrdinalIgnoreCase))
            return true;

        return IsGetUserByIdPath(path);
    }

    private static bool RequiresMenus(string method, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (path.Equals("/api/users/menus", StringComparison.OrdinalIgnoreCase))
            return HttpMethods.IsGet(method) || HttpMethods.IsPost(method);

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 4)
            return false;

        if (!segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!segments[1].Equals("users", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!segments[2].Equals("menus", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Guid.TryParse(segments[3], out _))
            return false;

        return HttpMethods.IsGet(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsDelete(method);
    }

    private static bool RequiresRoleMenus(string method, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (!HttpMethods.IsGet(method) && !HttpMethods.IsPut(method))
            return false;

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 5)
            return false;

        if (!segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!segments[1].Equals("users", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!segments[2].Equals("roles", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!segments[4].Equals("menus", StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParse(segments[3], out _);
    }

    private static bool IsGetUserByIdPath(string path)
    {
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3)
            return false;

        if (!segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!segments[1].Equals("users", StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParse(segments[2], out _);
    }
}
