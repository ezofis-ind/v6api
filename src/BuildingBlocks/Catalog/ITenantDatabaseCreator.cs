namespace SaaSApp.Catalog;

/// <summary>
/// Creates a new tenant database on the SQL Server (e.g. Azure SQL).
/// Requires a connection to "master" with permission to create databases.
/// </summary>
public interface ITenantDatabaseCreator
{
    /// <summary>
    /// Returns true if a database with the given name exists on the server.
    /// </summary>
    Task<bool> DatabaseExistsAsync(string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new database with the given name. Connection must be to the server (master or catalog) with CREATE DATABASE permission.
    /// </summary>
    Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
}
