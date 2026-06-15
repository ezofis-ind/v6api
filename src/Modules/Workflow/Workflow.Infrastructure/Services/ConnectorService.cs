using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Connectors;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>Connector CRUD against dbo.connector on tenant database (GUID id).</summary>
public sealed class ConnectorService : IConnectorService
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ConnectorService> _logger;

    public ConnectorService(
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        ILogger<ConnectorService> logger)
    {
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<ConnectorDto> CreateAsync(ConnectorUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var connectionString = RequireConnectionString();
        var userId = RequireUserId();
        var tenantId = RequireTenantId();
        var connectorId = Guid.NewGuid();
        var now = UtcNowString();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureConnectorSchemaAsync(connection, cancellationToken);

        var hasPreference = await HasColumnAsync(connection, "connector", "Preference", cancellationToken);
        var cols = new List<string> { "id", "name", "connectorType", "credentialJson", "dynamicCredentialJson",
            "responseStatus", "responseStatusCode", "responseBody",
            "createdAt", "modifiedAt", "createdBy", "modifiedBy", "isDeleted" };
        var vals = new List<string> { "@Id", "@Name", "@ConnectorType", "@CredentialJson", "@DynamicCredentialJson",
            "@ResponseStatus", "@ResponseStatusCode", "@ResponseBody",
            "@Now", "@Now", "@UserId", "@UserId", "0" };
        if (hasPreference)
        {
            cols.Add("Preference");
            vals.Add("@Preference");
        }

        var sql = $"""
            INSERT INTO dbo.connector ({string.Join(", ", cols)})
            VALUES ({string.Join(", ", vals)});
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", connectorId);
        AddUpsertParameters(cmd, request, hasPreference);
        cmd.Parameters.AddWithValue("@Now", now);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Created connector {ConnectorId} for tenant {TenantId}", connectorId, tenantId);
        return (await GetByIdCoreAsync(connection, tenantId, connectorId, cancellationToken))!;
    }

    public async Task<ConnectorDto?> UpdateAsync(
        Guid id,
        ConnectorUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        var connectionString = RequireConnectionString();
        var userId = RequireUserId();
        var tenantGuid = RequireTenantId();
        var now = UtcNowString();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureConnectorSchemaAsync(connection, cancellationToken);

        if (!await ConnectorExistsAsync(connection, id, cancellationToken))
            return null;

        var hasPreference = await HasColumnAsync(connection, "connector", "Preference", cancellationToken);
        var sets = new List<string> { "modifiedAt = @Now", "modifiedBy = @UserId" };
        await using var cmd = new SqlCommand { Connection = connection };
        AddIdParameter(cmd, "@Id", id);
        cmd.Parameters.AddWithValue("@Now", now);
        cmd.Parameters.AddWithValue("@UserId", userId);

        if (request.Name != null)
        {
            sets.Add("name = @Name");
            cmd.Parameters.AddWithValue("@Name", request.Name);
        }
        if (request.ConnectorType != null)
        {
            sets.Add("connectorType = @ConnectorType");
            cmd.Parameters.AddWithValue("@ConnectorType", request.ConnectorType);
        }
        if (request.CredentialJson != null)
        {
            sets.Add("credentialJson = @CredentialJson");
            cmd.Parameters.AddWithValue("@CredentialJson", request.CredentialJson);
        }
        if (request.DynamicCredentialJson != null)
        {
            sets.Add("dynamicCredentialJson = @DynamicCredentialJson");
            cmd.Parameters.AddWithValue("@DynamicCredentialJson", request.DynamicCredentialJson);
        }
        if (request.ResponseStatus != null)
        {
            sets.Add("responseStatus = @ResponseStatus");
            cmd.Parameters.AddWithValue("@ResponseStatus", request.ResponseStatus);
        }
        if (request.ResponseStatusCode != null)
        {
            sets.Add("responseStatusCode = @ResponseStatusCode");
            cmd.Parameters.AddWithValue("@ResponseStatusCode", request.ResponseStatusCode);
        }
        if (request.ResponseBody != null)
        {
            sets.Add("responseBody = @ResponseBody");
            cmd.Parameters.AddWithValue("@ResponseBody", request.ResponseBody);
        }
        if (hasPreference && request.Preference.HasValue)
        {
            sets.Add("Preference = @Preference");
            cmd.Parameters.AddWithValue("@Preference", request.Preference.Value);
        }

        cmd.CommandText = $"UPDATE dbo.connector SET {string.Join(", ", sets)} WHERE id = @Id AND isDeleted = 0;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Updated connector {ConnectorId}", id);
        return await GetByIdCoreAsync(connection, tenantGuid, id, cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectorDto>> ListAsync(
        ConnectorListRequest request,
        CancellationToken cancellationToken = default)
    {
        var connectionString = RequireConnectionString();
        var tenantGuid = RequireTenantId();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, "connector", cancellationToken))
            return Array.Empty<ConnectorDto>();

        await EnsureConnectorSchemaAsync(connection, cancellationToken);

        var showDeletedOnly = !string.Equals(request.Mode, "browse", StringComparison.OrdinalIgnoreCase);
        var whereParts = new List<string> { showDeletedOnly ? "c.isDeleted = 1" : "c.isDeleted = 0" };
        var parameters = new List<SqlParameter>();

        foreach (var group in request.FilterBy ?? new List<ConnectorFilterGroup>())
        {
            if (TryBuildFilterGroup(group, out var groupSql, out var groupParams))
            {
                whereParts.Add($"({groupSql})");
                parameters.AddRange(groupParams);
            }
        }

        var whereSql = string.Join(" AND ", whereParts);
        var sql = $"""
            SELECT
                c.id,
                c.name,
                c.connectorType,
                c.credentialJson,
                c.dynamicCredentialJson,
                c.responseStatus,
                c.responseStatusCode,
                c.responseBody,
                c.createdAt,
                c.modifiedAt,
                c.createdBy,
                c.modifiedBy,
                c.isDeleted,
                {(await HasColumnAsync(connection, "connector", "Preference", cancellationToken) ? "c.Preference" : "CAST(0 AS BIT)")} AS Preference,
                cb.Email AS CreatedByEmail,
                COALESCE(mb.Email, cb.Email) AS ModifiedByEmail
            FROM dbo.connector c
            LEFT JOIN users.Users cb ON cb.Id = TRY_CONVERT(uniqueidentifier, c.createdBy) AND cb.IsDeleted = 0
            LEFT JOIN users.Users mb ON mb.Id = TRY_CONVERT(uniqueidentifier, c.modifiedBy) AND mb.IsDeleted = 0
            WHERE {whereSql}
            ORDER BY c.createdAt, c.name;
            """;

        var items = new List<ConnectorDto>();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddRange(parameters.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(MapReader(reader, tenantGuid));

        return items;
    }

    public async Task<ConnectorDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        var connectionString = RequireConnectionString();
        var tenantGuid = RequireTenantId();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, "connector", cancellationToken))
            return null;

        await EnsureConnectorSchemaAsync(connection, cancellationToken);
        return await GetByIdCoreAsync(connection, tenantGuid, id, cancellationToken);
    }

    private async Task<ConnectorDto?> GetByIdCoreAsync(
        SqlConnection connection,
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var hasPreference = await HasColumnAsync(connection, "connector", "Preference", cancellationToken);
        var sql = $"""
            SELECT
                c.id,
                c.name,
                c.connectorType,
                c.credentialJson,
                c.dynamicCredentialJson,
                c.responseStatus,
                c.responseStatusCode,
                c.responseBody,
                c.createdAt,
                c.modifiedAt,
                c.createdBy,
                c.modifiedBy,
                c.isDeleted,
                {(hasPreference ? "c.Preference" : "CAST(0 AS BIT)")} AS Preference,
                cb.Email AS CreatedByEmail,
                COALESCE(mb.Email, cb.Email) AS ModifiedByEmail
            FROM dbo.connector c
            LEFT JOIN users.Users cb ON cb.Id = TRY_CONVERT(uniqueidentifier, c.createdBy) AND cb.IsDeleted = 0
            LEFT JOIN users.Users mb ON mb.Id = TRY_CONVERT(uniqueidentifier, c.modifiedBy) AND mb.IsDeleted = 0
            WHERE c.isDeleted = 0 AND c.id = @Id;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        AddIdParameter(cmd, "@Id", id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapReader(reader, tenantId) : null;
    }

    private static ConnectorDto MapReader(SqlDataReader reader, Guid tenantId) =>
        new(
            Id: ReadConnectorId(reader, 0),
            TenantId: tenantId,
            Name: reader.IsDBNull(1) ? null : reader.GetString(1),
            ConnectorType: reader.IsDBNull(2) ? null : reader.GetString(2),
            CredentialJson: reader.IsDBNull(3) ? null : reader.GetString(3),
            DynamicCredentialJson: reader.IsDBNull(4) ? null : reader.GetString(4),
            ResponseStatus: reader.IsDBNull(5) ? null : reader.GetString(5),
            ResponseStatusCode: reader.IsDBNull(6) ? null : reader.GetString(6),
            ResponseBody: reader.IsDBNull(7) ? null : reader.GetString(7),
            CreatedAt: reader.IsDBNull(8) ? null : reader.GetString(8),
            ModifiedAt: reader.IsDBNull(9) ? null : reader.GetString(9),
            CreatedBy: reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
            ModifiedBy: reader.IsDBNull(11) ? null : reader.GetString(11),
            IsDeleted: !reader.IsDBNull(12) && reader.GetBoolean(12),
            Preference: !reader.IsDBNull(13) && reader.GetBoolean(13),
            CreatedByEmail: reader.IsDBNull(14) ? null : reader.GetString(14),
            ModifiedByEmail: reader.IsDBNull(15) ? null : reader.GetString(15));

    private static Guid ReadConnectorId(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return Guid.Empty;

        return reader.GetValue(ordinal) switch
        {
            Guid guid => guid,
            string text when Guid.TryParse(text, out var parsed) => parsed,
            byte[] bytes when bytes.Length == 16 => new Guid(bytes),
            _ => throw new InvalidOperationException(
                "connector.id must be UNIQUEIDENTIFIER. Run scripts/Alter-Connector-Id-ToGuid.sql on the tenant database.")
        };
    }

    private static void AddIdParameter(SqlCommand cmd, string name, Guid id) =>
        cmd.Parameters.AddWithValue(name, id);

    private static void AddUpsertParameters(SqlCommand cmd, ConnectorUpsertRequest request, bool hasPreference)
    {
        cmd.Parameters.AddWithValue("@Name", (object?)request.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ConnectorType", (object?)request.ConnectorType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CredentialJson", (object?)request.CredentialJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DynamicCredentialJson", (object?)request.DynamicCredentialJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ResponseStatus", (object?)request.ResponseStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ResponseStatusCode", (object?)request.ResponseStatusCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ResponseBody", (object?)request.ResponseBody ?? DBNull.Value);
        if (hasPreference)
            cmd.Parameters.AddWithValue("@Preference", request.Preference ?? false);
    }

    private static bool TryBuildFilterGroup(
        ConnectorFilterGroup group,
        out string sql,
        out List<SqlParameter> parameters)
    {
        sql = string.Empty;
        parameters = new List<SqlParameter>();
        var parts = new List<string>();
        var logic = string.Equals(group.GroupCondition, "OR", StringComparison.OrdinalIgnoreCase) ? " OR " : " AND ";
        var index = 0;

        foreach (var filter in group.Filters)
        {
            if (!TryBuildFilterCondition(filter, index, out var condition, out var param))
                continue;
            parts.Add(condition);
            if (param != null)
                parameters.Add(param);
            index++;
        }

        if (parts.Count == 0)
            return false;

        sql = string.Join(logic, parts);
        return true;
    }

    private static bool TryBuildFilterCondition(
        ConnectorFilter filter,
        int index,
        out string conditionSql,
        out SqlParameter? parameter)
    {
        conditionSql = string.Empty;
        parameter = null;
        if (string.IsNullOrWhiteSpace(filter.Criteria) || string.IsNullOrWhiteSpace(filter.Condition))
            return false;

        var col = filter.Criteria.Trim().ToLowerInvariant() switch
        {
            "name" => "c.name",
            "connectortype" or "connector_type" => "c.connectorType",
            "createdby" => "c.createdBy",
            "modifiedby" => "c.modifiedBy",
            "responsestatus" => "c.responseStatus",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(col))
            return false;

        var paramName = $"@cf{index}";
        var cond = filter.Condition.Trim().ToLowerInvariant();
        if (cond is "contains" or "like")
        {
            conditionSql = $"{col} LIKE {paramName}";
            parameter = new SqlParameter(paramName, $"%{filter.Value ?? string.Empty}%");
            return true;
        }
        if (cond is "eq" or "=" or "equal")
        {
            conditionSql = $"{col} = {paramName}";
            parameter = new SqlParameter(paramName, filter.Value ?? string.Empty);
            return true;
        }
        if (cond is "neq" or "!=" or "notequal")
        {
            conditionSql = $"{col} <> {paramName}";
            parameter = new SqlParameter(paramName, filter.Value ?? string.Empty);
            return true;
        }

        return false;
    }

    private static async Task<bool> ConnectorExistsAsync(SqlConnection connection, Guid id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(1) FROM dbo.connector WHERE id = @Id AND isDeleted = 0;";
        await using var cmd = new SqlCommand(sql, connection);
        AddIdParameter(cmd, "@Id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task EnsureConnectorSchemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await EnsureConnectorTableAsync(connection, cancellationToken);
        await EnsureConnectorGuidIdAsync(connection, cancellationToken);
    }

    private static async Task EnsureConnectorTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(connection, "connector", cancellationToken))
            return;

        const string sql = """
            CREATE TABLE dbo.connector(
                id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                name NVARCHAR(500) NULL,
                connectorType NVARCHAR(500) NULL,
                credentialJson NVARCHAR(MAX) NULL,
                dynamicCredentialJson NVARCHAR(MAX) NULL,
                responseStatus NVARCHAR(50) NULL,
                responseStatusCode NVARCHAR(50) NULL,
                responseBody NVARCHAR(MAX) NULL,
                createdAt NVARCHAR(50) NULL,
                modifiedAt NVARCHAR(50) NULL,
                createdBy NVARCHAR(50) NOT NULL DEFAULT('0'),
                modifiedBy NVARCHAR(50) NOT NULL DEFAULT('0'),
                isDeleted BIT NOT NULL DEFAULT(0),
                Preference BIT NOT NULL DEFAULT(0)
            );
            CREATE INDEX IX_connector_isDeleted ON dbo.connector(isDeleted);
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureConnectorGuidIdAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var idType = await GetColumnTypeAsync(connection, "connector", "id", cancellationToken);
        if (idType is "uniqueidentifier")
            return;

        if (idType is not ("int" or "bigint" or "smallint" or "tinyint"))
            return;

        const string sql = """
            IF EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'connector'
                  AND COLUMN_NAME = 'id' AND DATA_TYPE IN ('int', 'bigint', 'smallint', 'tinyint'))
            AND NOT EXISTS (
                SELECT 1
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.tables pt ON fkc.referenced_object_id = pt.object_id
                INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
                WHERE pt.name = 'connector' AND rc.name = 'id' AND OBJECT_NAME(fk.parent_object_id) <> 'connector')
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'connector' AND COLUMN_NAME = 'id_new')
                BEGIN
                    ALTER TABLE dbo.connector ADD id_new UNIQUEIDENTIFIER NULL;
                    UPDATE dbo.connector SET id_new = NEWID() WHERE id_new IS NULL;

                    DECLARE @pk SYSNAME;
                    SELECT @pk = kc.name
                    FROM sys.key_constraints kc
                    INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
                    WHERE t.schema_id = SCHEMA_ID('dbo') AND t.name = 'connector' AND kc.type = 'PK';

                    IF @pk IS NOT NULL
                        EXEC(N'ALTER TABLE dbo.connector DROP CONSTRAINT ' + QUOTENAME(@pk));

                    ALTER TABLE dbo.connector DROP COLUMN id;
                    EXEC sp_rename N'dbo.connector.id_new', N'id', N'COLUMN';
                    ALTER TABLE dbo.connector ALTER COLUMN id UNIQUEIDENTIFIER NOT NULL;
                    ALTER TABLE dbo.connector ADD CONSTRAINT PK_connector PRIMARY KEY (id);
                END
            END
            """;

        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        idType = await GetColumnTypeAsync(connection, "connector", "id", cancellationToken);
        if (idType is not "uniqueidentifier")
        {
            throw new InvalidOperationException(
                "connector.id is still numeric (legacy v5). Run scripts/Alter-Connector-Id-ToGuid.sql on the tenant database, " +
                "or remove foreign keys from connectorHub.connectorId first.");
        }
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<bool> HasColumnAsync(
        SqlConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        cmd.Parameters.AddWithValue("@ColumnName", columnName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<string?> GetColumnTypeAsync(
        SqlConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 1 DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        cmd.Parameters.AddWithValue("@ColumnName", columnName);
        return (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString()?.ToLowerInvariant();
    }

    private string RequireConnectionString() =>
        _tenantContext.ConnectionString
        ?? throw new InvalidOperationException("Tenant connection string not resolved.");

    private Guid RequireTenantId() =>
        _tenantContext.TenantId
        ?? throw new InvalidOperationException("Tenant context is required.");

    private string RequireUserId() =>
        (_currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required."))
        .ToString("D");

    private static string UtcNowString() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}
