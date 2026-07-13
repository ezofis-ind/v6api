using Microsoft.Data.SqlClient;
using SaaSApp.ActivityLog.Application.Contracts;
using SaaSApp.MultiTenancy;

namespace SaaSApp.ActivityLog.Infrastructure.Services;

public sealed class ActivityLogQueryService : IActivityLogQueryService
{
    private readonly ITenantConnectionProvider _connectionProvider;

    public ActivityLogQueryService(ITenantConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<PagedResult<ActivityLogEntryDto>> ListAsync(
        Guid tenantId,
        ListActivityLogsQuery query,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;

        var where = new List<string> { "TenantId = @TenantId" };
        var parameters = new List<SqlParameter> { new("@TenantId", tenantId) };

        if (query.UserId.HasValue)
        {
            where.Add("UserId = @UserId");
            parameters.Add(new SqlParameter("@UserId", query.UserId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Method))
        {
            where.Add("HttpMethod = @HttpMethod");
            parameters.Add(new SqlParameter("@HttpMethod", query.Method.Trim().ToUpperInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(query.Path))
        {
            where.Add("Path LIKE @Path");
            parameters.Add(new SqlParameter("@Path", $"%{query.Path.Trim()}%"));
        }

        if (query.StatusCode.HasValue)
        {
            where.Add("StatusCode = @StatusCode");
            parameters.Add(new SqlParameter("@StatusCode", query.StatusCode.Value));
        }

        if (query.DateFrom.HasValue)
        {
            where.Add("CreatedAtUtc >= @DateFrom");
            parameters.Add(new SqlParameter("@DateFrom", query.DateFrom.Value));
        }

        if (query.DateTo.HasValue)
        {
            where.Add("CreatedAtUtc <= @DateTo");
            parameters.Add(new SqlParameter("@DateTo", query.DateTo.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            where.Add("CorrelationId = @CorrelationId");
            parameters.Add(new SqlParameter("@CorrelationId", query.CorrelationId.Trim()));
        }

        var whereClause = string.Join(" AND ", where);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var countSql = $"SELECT COUNT(1) FROM activitylog.ApiAccessLogs WHERE {whereClause}";
        await using var countCmd = new SqlCommand(countSql, connection);
        AddParameters(countCmd, parameters);
        var total = (int)(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0);

        var dataSql = $"""
            SELECT Id, TenantId, UserId, UserEmail, HttpMethod, Path, QueryString,
                   StatusCode, DurationMs, CorrelationId, ClientIp, UserAgent, CreatedAtUtc
            FROM activitylog.ApiAccessLogs
            WHERE {whereClause}
            ORDER BY CreatedAtUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        await using var dataCmd = new SqlCommand(dataSql, connection);
        AddParameters(dataCmd, parameters);
        dataCmd.Parameters.AddWithValue("@Offset", offset);
        dataCmd.Parameters.AddWithValue("@PageSize", pageSize);

        var list = new List<ActivityLogEntryDto>();
        await using var reader = await dataCmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new ActivityLogEntryDto(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetDateTime(12)));
        }

        return new PagedResult<ActivityLogEntryDto>(list, page, pageSize, total);
    }

    private static void AddParameters(SqlCommand command, IReadOnlyList<SqlParameter> parameters)
    {
        foreach (var p in parameters)
            command.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
    }
}
