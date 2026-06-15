using Microsoft.EntityFrameworkCore;
using SaaSApp.Catalog.Entities;

namespace SaaSApp.Catalog.Persistence;

public sealed class CatalogDbContext : DbContext
{
    public const string SchemaName = "catalog";

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserTenant> UserTenants => Set<UserTenant>();
    public DbSet<MailSetting> MailSettings => Set<MailSetting>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ConnectionString).IsRequired();
            entity.Property(e => e.IsActive);
            entity.Property(e => e.CreatedAtUtc);
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.SignupSource).HasMaxLength(128);
            entity.Property(e => e.Platform).HasMaxLength(64);
            entity.Property(e => e.AppVersion).HasMaxLength(64);
            entity.Property(e => e.LoginType).HasMaxLength(64);
        });

        modelBuilder.Entity<UserTenant>(entity =>
        {
            entity.ToTable("UserTenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CreatedAtUtc);
            entity.HasIndex(e => new { e.Email, e.TenantId }).IsUnique();
        });

        modelBuilder.Entity<MailSetting>(entity =>
        {
            entity.ToTable("mailsettings", "dbo");
            entity.HasKey(e => e.SettingId);
            entity.Property(e => e.SettingId).HasColumnName("settingId");
            entity.Property(e => e.EmailId).HasColumnName("EmailId");
            entity.Property(e => e.Password).HasColumnName("Password");
            entity.Property(e => e.OutgoingServer).HasColumnName("OutgoingServer");
            entity.Property(e => e.OutgoingPort).HasColumnName("OutgoingPort");
            entity.Property(e => e.Isdeleted).HasColumnName("Isdeleted");
            entity.Property(e => e.Preference).HasColumnName("Preference");
        });

        modelBuilder.Entity<OtpVerification>(entity =>
        {
            entity.ToTable("OTPVerification", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.OTP).HasColumnName("OTP");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.ValidateAt).HasColumnName("validateAt");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.ModifiedAt).HasColumnName("modifiedAt");
            entity.Property(e => e.IsDeleted).HasColumnName("isDeleted");
        });
    }
}
