using Microsoft.EntityFrameworkCore;
using SaaSApp.Catalog.Persistence;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Catalog;

public sealed class TenantConnectionStringResolver : ITenantConnectionStringResolver
{
    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;

    public TenantConnectionStringResolver(IDbContextFactory<CatalogDbContext> catalogFactory)
    {
        _catalogFactory = catalogFactory;
    }

    public async Task<string?> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
        var tenant = await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && t.IsActive)
            .Select(t => t.ConnectionString)
            .FirstOrDefaultAsync(cancellationToken);
        return tenant;
    }
}
