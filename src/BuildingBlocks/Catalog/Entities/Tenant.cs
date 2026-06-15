namespace SaaSApp.Catalog.Entities;

/// <summary>
/// Tenant registry: maps TenantId to its dedicated database connection string.
/// Stored in the catalog (main) database.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string ConnectionString { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? Email { get; set; }
    public string? SignupSource { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public string? LoginType { get; set; }
}
