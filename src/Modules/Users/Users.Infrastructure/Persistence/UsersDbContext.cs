using Microsoft.EntityFrameworkCore;
using SaaSApp.MultiTenancy;
using SaaSApp.SharedKernel.Domain;
using SaaSApp.Users.Domain;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Infrastructure.Persistence;

public sealed class UsersDbContext : DbContext
{
    public const string SchemaName = "users";

    private readonly ITenantProvider _tenantProvider;

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<PermissionCategory> PermissionCategories => Set<PermissionCategory>();

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
            entity.Property(e => e.Role).HasMaxLength(128);
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

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.Property(e => e.CreatedAtUtc);
            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
            entity.HasMany(e => e.UserRoles).WithOne(ur => ur.Role).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Permissions).WithOne(rp => rp.Role).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Menus).WithOne(rm => rm.Role).HasForeignKey(rm => rm.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(e => e.UserRoles).HasField("_userRoles");
            entity.Navigation(e => e.Permissions).HasField("_permissions");
            entity.Navigation(e => e.Menus).HasField("_menus");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(e => new { e.RoleId, e.UserId });
            entity.Property(e => e.TenantId);
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.ToTable("Groups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.Property(e => e.CreatedAtUtc);
            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
            entity.HasMany(e => e.UserGroups).WithOne(ug => ug.Group).HasForeignKey(ug => ug.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(e => e.UserGroups).HasField("_userGroups");
        });

        modelBuilder.Entity<Menu>(entity =>
        {
            entity.ToTable("Menus");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(128).IsRequired();
            entity.Property(e => e.RoutePath).HasMaxLength(256).IsRequired();
            entity.Property(e => e.SortOrder);
            entity.Property(e => e.IsSystem);
            entity.Property(e => e.CreatedAtUtc);
            entity.HasIndex(e => e.Key).IsUnique();

            foreach (var (id, key, label, routePath, sortOrder) in MenuDefaults.All)
            {
                entity.HasData(new Menu(id, key, label, routePath, sortOrder, isSystem: true));
            }
        });

        modelBuilder.Entity<RoleMenu>(entity =>
        {
            entity.ToTable("RoleMenus");
            entity.HasKey(e => new { e.RoleId, e.MenuId });
            entity.Property(e => e.TenantId);
            entity.Property(e => e.IsDefaultLanding);
            entity.HasOne(e => e.Role).WithMany(r => r.Menus).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Menu).WithMany().HasForeignKey(e => e.MenuId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserGroup>(entity =>
        {
            entity.ToTable("UserGroups");
            entity.HasKey(e => new { e.GroupId, e.UserId });
            entity.Property(e => e.TenantId);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(e => new { e.RoleId, e.PermissionKey });
            entity.Property(e => e.PermissionKey).HasMaxLength(128).IsRequired();
            entity.Property(e => e.TenantId);
        });

        modelBuilder.Entity<PermissionCategory>(entity =>
        {
            entity.ToTable("PermissionCategories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.SortOrder);
            entity.HasIndex(e => e.Key).IsUnique();

            foreach (var (id, key, name, sortOrder) in PermissionCategoryDefaults.All)
            {
                entity.HasData(new PermissionCategory(id, key, name, sortOrder));
            }
        });

        // Global query filter: only data for current tenant, exclude soft-deleted
        modelBuilder.Entity<User>().HasQueryFilter(e =>
            e.TenantId == (_tenantProvider.GetTenantId() ?? Guid.Empty) && !e.IsDeleted);

        modelBuilder.Entity<Role>().HasQueryFilter(e =>
            e.TenantId == (_tenantProvider.GetTenantId() ?? Guid.Empty) && !e.IsDeleted);

        modelBuilder.Entity<UserRole>().HasQueryFilter(e =>
            e.TenantId == (_tenantProvider.GetTenantId() ?? Guid.Empty));

        modelBuilder.Entity<Group>().HasQueryFilter(e =>
            e.TenantId == (_tenantProvider.GetTenantId() ?? Guid.Empty) && !e.IsDeleted);

        modelBuilder.Entity<Menu>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<RoleMenu>().HasQueryFilter(e =>
            e.TenantId == (_tenantProvider.GetTenantId() ?? Guid.Empty));

        modelBuilder.Entity<UserGroup>().HasQueryFilter(e =>
            e.TenantId == (_tenantProvider.GetTenantId() ?? Guid.Empty));

        modelBuilder.Entity<RolePermission>().HasQueryFilter(e =>
            e.TenantId == (_tenantProvider.GetTenantId() ?? Guid.Empty));
    }

}
