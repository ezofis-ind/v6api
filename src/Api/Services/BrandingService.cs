using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SaaSApp.Catalog.Entities;
using SaaSApp.Catalog.Persistence;

namespace SaaSApp.Api.Services;

public interface IBrandingService
{
    Task<BrandingDto> SaveAsync(SaveBrandingRequest request, CancellationToken cancellationToken = default);
    Task<BrandingDto?> GetByBrandingNameAsync(string brandingName, CancellationToken cancellationToken = default);
}

public sealed record SaveBrandingRequest(
    Guid TenantId,
    Guid UserId,
    string UserEmail,
    string BrandingName,
    string BrandingJson);

public sealed record BrandingDto(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string UserEmail,
    string BrandingName,
    string BrandingJson,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed class BrandingService : IBrandingService
{
    private readonly IDbContextFactory<CatalogDbContext> _dbFactory;
    private readonly IConfiguration _configuration;

    public BrandingService(
        IDbContextFactory<CatalogDbContext> dbFactory,
        IConfiguration configuration)
    {
        _dbFactory = dbFactory;
        _configuration = configuration;
    }

    public async Task<BrandingDto> SaveAsync(SaveBrandingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.");
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("UserId is required.");
        if (string.IsNullOrWhiteSpace(request.UserEmail))
            throw new ArgumentException("UserEmail is required.");
        if (string.IsNullOrWhiteSpace(request.BrandingName))
            throw new ArgumentException("BrandingName is required.");

        await EnsureSchemaAsync(cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var name = request.BrandingName.Trim();
        var existing = await db.Brandings
            .Where(b => !b.IsDeleted
                        && b.TenantId == request.TenantId
                        && b.BrandingName == name)
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
        {
            existing.UserId = request.UserId;
            existing.UserEmail = request.UserEmail.Trim();
            existing.BrandingJson = string.IsNullOrWhiteSpace(request.BrandingJson) ? "{}" : request.BrandingJson;
            existing.ModifiedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Map(existing);
        }

        var row = new Branding
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            UserId = request.UserId,
            UserEmail = request.UserEmail.Trim(),
            BrandingName = name,
            BrandingJson = string.IsNullOrWhiteSpace(request.BrandingJson) ? "{}" : request.BrandingJson,
            CreatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };
        db.Brandings.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<BrandingDto?> GetByBrandingNameAsync(string brandingName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(brandingName))
            return null;

        await EnsureSchemaAsync(cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var name = brandingName.Trim();
        var row = await db.Brandings
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.BrandingName == name)
            .OrderByDescending(b => b.ModifiedAtUtc ?? b.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return row == null ? null : Map(row);
    }

    private static BrandingDto Map(Branding row) =>
        new(row.Id, row.TenantId, row.UserId, row.UserEmail, row.BrandingName, row.BrandingJson, row.CreatedAtUtc, row.ModifiedAtUtc);

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        var cs = _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration.GetConnectionString("CatalogConnection")
            ?? throw new InvalidOperationException("Catalog connection string not configured.");

        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(EnsureSchemaSql, connection) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string EnsureSchemaSql = """
        IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'catalog')
            EXEC(N'CREATE SCHEMA catalog');

        IF NOT EXISTS (
            SELECT 1 FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'catalog' AND t.name = N'Branding')
        BEGIN
            CREATE TABLE catalog.Branding (
                Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Branding PRIMARY KEY,
                TenantId        UNIQUEIDENTIFIER NOT NULL,
                UserId          UNIQUEIDENTIFIER NOT NULL,
                UserEmail       NVARCHAR(256) NOT NULL,
                BrandingName    NVARCHAR(256) NOT NULL,
                BrandingJson    NVARCHAR(MAX) NOT NULL,
                CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_Branding_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                ModifiedAtUtc   DATETIME2(3) NULL,
                IsDeleted       BIT NOT NULL CONSTRAINT DF_Branding_IsDeleted DEFAULT (0)
            );
            CREATE INDEX IX_Branding_Name ON catalog.Branding (BrandingName) WHERE IsDeleted = 0;
            CREATE INDEX IX_Branding_Tenant_Name ON catalog.Branding (TenantId, BrandingName) WHERE IsDeleted = 0;
        END
        """;
}
