using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Users.Infrastructure.Persistence;

/// <summary>
/// Enables <c>dotnet ef database update</c> using ConnectionStrings__DefaultConnection from the environment.
/// </summary>
public sealed class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("TenantConnectionString")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection or TenantConnectionString before running EF tools.");

        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.SchemaName);
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });

        return new UsersDbContext(optionsBuilder.Options, new StaticTenantProvider(Guid.Empty));
    }
}
