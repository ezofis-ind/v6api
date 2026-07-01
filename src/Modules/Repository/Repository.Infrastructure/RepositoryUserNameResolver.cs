using Microsoft.Data.SqlClient;

namespace SaaSApp.Repository.Infrastructure;

internal static class RepositoryUserNameResolver
{
    public static async Task<string?> ResolveEmailAsync(
        SqlConnection connection,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return null;

        const string sql = """
            SELECT Email
            FROM users.Users
            WHERE Id = @UserId AND IsDeleted = 0;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return reader.IsDBNull(0) ? null : reader.GetString(0).Trim();
    }

    public static bool TryParseUserId(object? value, out Guid userId)
    {
        userId = Guid.Empty;
        if (value == null)
            return false;

        if (value is Guid guid)
        {
            userId = guid;
            return userId != Guid.Empty;
        }

        return Guid.TryParse(value.ToString(), out userId) && userId != Guid.Empty;
    }
}
