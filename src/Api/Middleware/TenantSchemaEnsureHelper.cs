using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace SaaSApp.Api.Middleware;

/// <summary>
/// Fast-path schema ensure: skip multi-thousand-line DDL when marker tables already exist.
/// Uses per-tenant semaphores to avoid duplicate apply on concurrent first requests.
/// </summary>
internal static class TenantSchemaEnsureHelper
{
    private static readonly ConcurrentDictionary<string, byte> Applied = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

    public static Task EnsureWorkflowSchemaAsync(
        Guid tenantId,
        string connectionString,
        Func<Task> applySchema,
        CancellationToken cancellationToken) =>
        EnsureOnceAsync(
            tenantId,
            "workflow",
            connectionString,
            "SELECT 1 FROM sys.tables WHERE name = N'Workflows' AND schema_id = SCHEMA_ID(N'workflow')",
            applySchema,
            cancellationToken);

    public static Task EnsureDmsSchemaAsync(
        Guid tenantId,
        string connectionString,
        Func<Task> applySchema,
        CancellationToken cancellationToken) =>
        EnsureOnceAsync(
            tenantId,
            "dms",
            connectionString,
            "SELECT 1 FROM sys.tables WHERE name = N'Repository' AND schema_id = SCHEMA_ID(N'dms')",
            applySchema,
            cancellationToken);

    public static Task EnsureRepositorySchemaAsync(
        Guid tenantId,
        string connectionString,
        Func<Task> applySchema,
        CancellationToken cancellationToken) =>
        EnsureOnceAsync(
            tenantId,
            "repository",
            connectionString,
            "SELECT 1 FROM sys.tables WHERE name = N'Repositories' AND schema_id = SCHEMA_ID(N'repository')",
            applySchema,
            cancellationToken);

    public static Task EnsurePermissionCategoriesAsync(
        Guid tenantId,
        string connectionString,
        Func<Task> applySchema,
        CancellationToken cancellationToken) =>
        EnsureOnceAsync(
            tenantId,
            "users-permission-categories",
            connectionString,
            "SELECT 1 FROM sys.tables WHERE name = N'PermissionCategories' AND schema_id = SCHEMA_ID(N'users')",
            applySchema,
            cancellationToken);

    public static Task EnsureMenusTablesAsync(
        Guid tenantId,
        string connectionString,
        Func<Task> applySchema,
        CancellationToken cancellationToken) =>
        EnsureOnceAsync(
            tenantId,
            "users-menus",
            connectionString,
            "SELECT 1 FROM sys.tables WHERE name = N'Menus' AND schema_id = SCHEMA_ID(N'users')",
            applySchema,
            cancellationToken);

    public static Task EnsureRoleMenusTablesAsync(
        Guid tenantId,
        string connectionString,
        Func<Task> applySchema,
        CancellationToken cancellationToken) =>
        EnsureOnceAsync(
            tenantId,
            "users-role-menus",
            connectionString,
            "SELECT 1 FROM sys.tables WHERE name = N'RoleMenus' AND schema_id = SCHEMA_ID(N'users')",
            applySchema,
            cancellationToken);

    public static Task EnsureExtendedUserColumnsAsync(
        Guid tenantId,
        string connectionString,
        Func<Task> applySchema,
        CancellationToken cancellationToken) =>
        EnsureOnceAsync(
            tenantId,
            "users-extended-columns",
            connectionString,
            """
            SELECT 1
            WHERE COL_LENGTH('users.Users', 'PasswordExpiryDays') IS NOT NULL
              AND COL_LENGTH('users.Users', 'AccountExpiryDate') IS NOT NULL
              AND COL_LENGTH('users.Users', 'ForcePasswordResetOnLogin') IS NOT NULL
              AND COL_LENGTH('users.Users', 'EmployeeId') IS NOT NULL
              AND COL_LENGTH('users.Users', 'BusinessUnit') IS NOT NULL
              AND COL_LENGTH('users.Users', 'Location') IS NOT NULL
              AND COL_LENGTH('users.Users', 'GroupName') IS NOT NULL
              AND COL_LENGTH('users.Users', 'MfaMethods') IS NOT NULL
            """,
            applySchema,
            cancellationToken);

    private static async Task EnsureOnceAsync(
        Guid tenantId,
        string schemaKey,
        string connectionString,
        string existsSql,
        Func<Task> applySchema,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{tenantId:N}:{schemaKey}";
        if (Applied.ContainsKey(cacheKey))
            return;

        if (await SchemaMarkerExistsAsync(connectionString, existsSql, cancellationToken))
        {
            Applied.TryAdd(cacheKey, 0);
            return;
        }

        var gate = Locks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (Applied.ContainsKey(cacheKey))
                return;

            if (await SchemaMarkerExistsAsync(connectionString, existsSql, cancellationToken))
            {
                Applied.TryAdd(cacheKey, 0);
                return;
            }

            await applySchema();
            Applied.TryAdd(cacheKey, 0);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<bool> SchemaMarkerExistsAsync(
        string connectionString,
        string existsSql,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(existsSql, connection) { CommandTimeout = 5 };
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null && result != DBNull.Value;
    }
}
