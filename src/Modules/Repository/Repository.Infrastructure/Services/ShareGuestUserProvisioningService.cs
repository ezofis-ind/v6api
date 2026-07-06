using Microsoft.EntityFrameworkCore;
using SaaSApp.Catalog;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Users.Domain.Entities;
using SaaSApp.Users.Infrastructure.Persistence;

namespace SaaSApp.Repository.Infrastructure.Services;

public sealed class ShareGuestUserProvisioningService : IShareGuestUserProvisioningService
{
    private readonly ITenantConnectionStringResolver _connectionResolver;
    private readonly IUserTenantRegistry _userTenantRegistry;

    public ShareGuestUserProvisioningService(
        ITenantConnectionStringResolver connectionResolver,
        IUserTenantRegistry userTenantRegistry)
    {
        _connectionResolver = connectionResolver;
        _userTenantRegistry = userTenantRegistry;
    }

    public async Task<Guid> EnsureGuestUserAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        await using var context = await CreateUsersContextAsync(tenantId, cancellationToken);

        var existing = await context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (existing != null)
        {
            await _userTenantRegistry.AddOrUpdateAsync(normalizedEmail, tenantId, existing.Role, cancellationToken);
            return existing.Id;
        }

        var displayName = normalizedEmail.Split('@')[0];
        var user = User.Create(
            tenantId,
            normalizedEmail,
            displayName,
            User.RoleTenantUser,
            authStrategy: User.AuthStrategyEzofis);
        user.SetLoginType("EZOFIS");

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        await _userTenantRegistry.AddOrUpdateAsync(normalizedEmail, tenantId, User.RoleTenantUser, cancellationToken);
        return user.Id;
    }

    public async Task<bool> RequiresPasswordSetupAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        await using var context = await CreateUsersContextAsync(tenantId, cancellationToken);
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        return user != null && string.IsNullOrEmpty(user.PasswordHash);
    }

    public async Task<bool> SetFirstPasswordAsync(
        Guid tenantId,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.");

        var normalizedEmail = NormalizeEmail(email);
        await using var context = await CreateUsersContextAsync(tenantId, cancellationToken);
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user == null)
            return false;

        if (!string.IsNullOrEmpty(user.PasswordHash))
            throw new InvalidOperationException("Password is already set. Use login instead.");

        user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(password.Trim()));
        user.SetLoginType("EZOFIS");
        await context.SaveChangesAsync(cancellationToken);
        await _userTenantRegistry.AddOrUpdateAsync(normalizedEmail, tenantId, user.Role, cancellationToken);
        return true;
    }

    private async Task<UsersDbContext> CreateUsersContextAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connectionString = await _connectionResolver.GetConnectionStringAsync(tenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Tenant connection string not found.");

        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.SchemaName);
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
        });

        return new UsersDbContext(optionsBuilder.Options, new StaticTenantProvider(tenantId));
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = email?.Trim().ToLowerInvariant()
            ?? throw new ArgumentException("Email is required.");
        if (normalized.IndexOf('@') < 1)
            throw new ArgumentException("A valid email is required.");
        return normalized;
    }
}
