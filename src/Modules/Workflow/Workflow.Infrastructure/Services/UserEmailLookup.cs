using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class UserEmailLookup : IUserEmailLookup
{
    private readonly ITenantContext _tenantContext;

    public UserEmailLookup(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        var parameters = ids.Select((id, index) => new SqlParameter($"@id{index}", id)).ToArray();
        var inList = string.Join(", ", parameters.Select(p => p.ParameterName));
        var sql = $"""
            SELECT Id, Email
            FROM users.Users
            WHERE IsDeleted = 0 AND Id IN ({inList});
            """;

        var map = new Dictionary<Guid, string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddRange(parameters);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            map[reader.GetGuid(0)] = reader.GetString(1);

        return map;
    }
}
