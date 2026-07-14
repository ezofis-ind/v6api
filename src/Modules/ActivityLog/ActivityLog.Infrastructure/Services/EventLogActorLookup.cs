using Microsoft.Data.SqlClient;

namespace SaaSApp.ActivityLog.Infrastructure.Services;

/// <summary>Best-effort tenant lookups for Event Log title enrichment. Never throws.</summary>
public static class EventLogActorLookup
{
    public static async Task<(string? DisplayName, string? Email)> TryGetUserAsync(
        string? connectionString,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || userId == Guid.Empty)
            return (null, null);

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                SELECT TOP 1 DisplayName, Email
                FROM users.Users
                WHERE Id = @Id
                """;

            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
            command.Parameters.AddWithValue("@Id", userId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return (null, null);

            var displayName = reader.IsDBNull(0) ? null : reader.GetString(0);
            var email = reader.IsDBNull(1) ? null : reader.GetString(1);
            return (
                string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                string.IsNullOrWhiteSpace(email) ? null : email.Trim());
        }
        catch
        {
            return (null, null);
        }
    }

    public static async Task<string?> TryGetRoleNameAsync(
        string? connectionString,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || roleId == Guid.Empty)
            return null;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                SELECT TOP 1 Name
                FROM users.Roles
                WHERE Id = @Id AND IsDeleted = 0
                """;

            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
            command.Parameters.AddWithValue("@Id", roleId);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var name = result as string;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch
        {
            return null;
        }
    }
}
