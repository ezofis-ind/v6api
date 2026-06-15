using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaaSApp.Catalog.Entities;
using SaaSApp.Catalog.Persistence;

namespace SaaSApp.Catalog;

public sealed class UserTenantRegistry : IUserTenantRegistry
{
    private const int SqlErrorInvalidObjectName = 208;

    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;

    public UserTenantRegistry(IDbContextFactory<CatalogDbContext> catalogFactory)
    {
        _catalogFactory = catalogFactory;
    }

    public async Task AddOrUpdateAsync(string email, Guid tenantId, string role, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email?.Trim() ?? throw new ArgumentNullException(nameof(email));
        if (string.IsNullOrEmpty(normalizedEmail))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        try
        {
            await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
            var existing = await context.UserTenants
                .FirstOrDefaultAsync(ut => ut.Email == normalizedEmail && ut.TenantId == tenantId, cancellationToken);

            if (existing != null)
            {
                existing.Role = role?.Trim() ?? existing.Role;
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            context.UserTenants.Add(new UserTenant
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                TenantId = tenantId,
                Role = role?.Trim() ?? "TenantUser",
                CreatedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorInvalidObjectName)
        {
            // catalog.UserTenants table not created yet; run migration AddUserTenants or scripts/AddUserTenantsTable.sql
        }
    }
}
