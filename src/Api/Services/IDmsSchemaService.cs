namespace SaaSApp.Api.Services;

/// <summary>Applies DMS schema (dms.Repository, sample_items, etc.) to a tenant database.</summary>
public interface IDmsSchemaService
{
    /// <summary>Apply DMS schema to the given connection string. Idempotent.</summary>
    Task ApplySchemaAsync(string connectionString, CancellationToken cancellationToken = default);
}
