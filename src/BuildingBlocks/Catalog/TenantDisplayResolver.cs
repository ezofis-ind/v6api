using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SaaSApp.Catalog.Persistence;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Catalog;

public sealed class TenantDisplayResolver : ITenantDisplayResolver
{
    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;
    private readonly IMemoryCache _cache;

    public TenantDisplayResolver(
        IDbContextFactory<CatalogDbContext> catalogFactory,
        IMemoryCache cache)
    {
        _catalogFactory = catalogFactory;
        _cache = cache;
    }

    public async Task<string> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"tenant-display:{tenantId:D}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
            return cached;

        await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
        var name = await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var display = string.IsNullOrWhiteSpace(name) ? tenantId.ToString("D") : name.Trim();
        _cache.Set(cacheKey, display, TimeSpan.FromMinutes(10));
        return display;
    }
}
