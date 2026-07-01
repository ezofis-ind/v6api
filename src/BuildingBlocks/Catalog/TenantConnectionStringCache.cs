using Microsoft.Extensions.Caching.Memory;

namespace SaaSApp.Catalog;

internal static class TenantConnectionStringCache
{
    private const string Prefix = "tenant-conn:";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    public static bool TryGet(IMemoryCache cache, Guid tenantId, out string? connectionString)
    {
        if (cache.TryGetValue(Prefix + tenantId.ToString("D"), out string? cached)
            && !string.IsNullOrWhiteSpace(cached))
        {
            connectionString = cached;
            return true;
        }

        connectionString = null;
        return false;
    }

    public static void Set(IMemoryCache cache, Guid tenantId, string connectionString) =>
        cache.Set(Prefix + tenantId.ToString("D"), connectionString, Ttl);
}
