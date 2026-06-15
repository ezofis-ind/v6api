using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SaaSApp.Catalog;

public sealed class TenantDatabaseCreator : ITenantDatabaseCreator
{
    private readonly string _masterConnectionString;

    /// <summary>Command timeout in seconds for CREATE DATABASE (Azure SQL can take 60+ seconds).</summary>
    private const int CreateDatabaseCommandTimeoutSeconds = 180;

    public TenantDatabaseCreator(IConfiguration configuration)
    {
        var catalogConnection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not found.");
        var builder = new SqlConnectionStringBuilder(catalogConnection) { InitialCatalog = "master" };
        if (builder.ConnectTimeout < 60)
            builder.ConnectTimeout = 60;
        _masterConnectionString = builder.ConnectionString;
    }

    public async Task<bool> DatabaseExistsAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            return false;
        var safeName = new string(databaseName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrEmpty(safeName))
            return false;
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sys.databases WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", safeName);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    public async Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required.", nameof(databaseName));
        if (databaseName.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Database name must be alphanumeric or underscore.", nameof(databaseName));

        var safeName = new string(databaseName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrEmpty(safeName))
            throw new ArgumentException("Invalid database name.", nameof(databaseName));

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{safeName}]";
        cmd.CommandTimeout = CreateDatabaseCommandTimeoutSeconds;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
