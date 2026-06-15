using Microsoft.EntityFrameworkCore;
using SaaSApp.MultiTenancy;
using SaaSApp.SharedKernel.Domain;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Infrastructure.Persistence;

public sealed class UsersDbContext : DbContext
{
    public const string SchemaName = "users";

    private readonly ITenantProvider _tenantProvider;

    public DbSet<User> Users => Set<User>();

    public UsersDbContext(DbContextOptions<UsersDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.TenantId);
            entity.Property(e => e.Role).HasMaxLength(64);
            entity.Property(e => e.CreatedAtUtc);
            entity.Property(e => e.FirstName).HasMaxLength(128);
            entity.Property(e => e.LastName).HasMaxLength(128);
            entity.Property(e => e.ProfileId).HasMaxLength(128);
            entity.Property(e => e.PhoneNo).HasMaxLength(32);
            entity.Property(e => e.SecondaryEmail).HasMaxLength(256);
            entity.Property(e => e.Language).HasMaxLength(16);
            entity.Property(e => e.CountryCode).HasMaxLength(8);
            entity.Property(e => e.Department).HasMaxLength(128);
            entity.Property(e => e.JobTitle).HasMaxLength(128);
            entity.Property(e => e.UserType).HasMaxLength(64);
            entity.Property(e => e.AuthStrategy).HasMaxLength(64);
            entity.Property(e => e.LoginType).HasMaxLength(64);
            entity.Property(e => e.LoginName).HasMaxLength(256);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
            entity.Property(e => e.PinHash).HasMaxLength(256);
            entity.Property(e => e.DeviceId).HasMaxLength(256);
            entity.Property(e => e.TotpSecretEncrypted).HasMaxLength(512);
            entity.Property(e => e.PasswordAge);
            entity.Property(e => e.GoogleSubjectId).HasMaxLength(256);
            entity.Property(e => e.MicrosoftOid).HasMaxLength(256);
            entity.Property(e => e.AvatarPath).HasMaxLength(512);
            entity.Property(e => e.IdCardPath).HasMaxLength(512);
            entity.Property(e => e.SignaturePath).HasMaxLength(512);
            entity.Property(e => e.UiPreference).HasMaxLength(512);
            entity.Property(e => e.ModifiedAtUtc);
        });

        // Global query filter: only data for current tenant, exclude soft-deleted
        modelBuilder.Entity<User>().HasQueryFilter(e =>
            e.TenantId == (_tenantProvider.GetTenantId() ?? Guid.Empty) && !e.IsDeleted);
    }

}
