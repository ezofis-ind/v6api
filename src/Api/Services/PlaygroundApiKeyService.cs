using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaaSApp.Catalog.Persistence;

namespace SaaSApp.Api.Services;

public sealed class PlaygroundApiKeyService : IPlaygroundApiKeyService
{
    private static readonly ConcurrentDictionary<string, byte> TenantSchemaApplied = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> CatalogSchemaApplied = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim CatalogSchemaLock = new(1, 1);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantSchemaLocks = new();

    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlaygroundApiKeyService> _logger;

    public PlaygroundApiKeyService(
        IDbContextFactory<CatalogDbContext> catalogFactory,
        IConfiguration configuration,
        ILogger<PlaygroundApiKeyService> logger)
    {
        _catalogFactory = catalogFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PlaygroundApiKeyDto> CreateAsync(
        Guid tenantId,
        string tenantConnectionString,
        CreatePlaygroundApiKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogSchemaAsync(cancellationToken);
        await EnsureTenantSchemaAsync(tenantId, tenantConnectionString, cancellationToken);

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var email = request.Email.Trim();
        var apiKey = request.ApiKey.Trim();
        var label = string.IsNullOrWhiteSpace(request.KeyLabel) ? "Playground key" : request.KeyLabel.Trim();

        await using (var conn = new SqlConnection(tenantConnectionString))
        {
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(
                @"INSERT INTO dbo.playgroundApiKeys
                  (Id, Email, ApiKey, KeyLabel, ProtectedPassword, CreatedAtUtc, ExpiresAtUtc, IsActive)
                  VALUES (@id, @email, @apiKey, @label, @pwd, @created, @expires, 1)", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@apiKey", apiKey);
            cmd.Parameters.AddWithValue("@label", label);
            cmd.Parameters.AddWithValue("@pwd", (object?)request.ProtectedPassword ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", now);
            cmd.Parameters.AddWithValue("@expires", (object?)request.ExpiresAtUtc ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var catalogCs = GetCatalogConnectionString();
        await using (var conn = new SqlConnection(catalogCs))
        {
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(
                @"MERGE catalog.PlaygroundApiKeyRoutes AS target
                  USING (SELECT @apiKey AS ApiKey) AS source
                  ON target.ApiKey = source.ApiKey
                  WHEN MATCHED THEN
                    UPDATE SET TenantId = @tenantId, KeyId = @keyId, Email = @email, IsActive = 1, CreatedAtUtc = @created
                  WHEN NOT MATCHED THEN
                    INSERT (ApiKey, TenantId, KeyId, Email, IsActive, CreatedAtUtc)
                    VALUES (@apiKey, @tenantId, @keyId, @email, 1, @created);", conn);
            cmd.Parameters.AddWithValue("@apiKey", apiKey);
            cmd.Parameters.AddWithValue("@tenantId", tenantId);
            cmd.Parameters.AddWithValue("@keyId", id);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@created", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return new PlaygroundApiKeyDto
        {
            Id = id,
            TenantId = tenantId,
            Email = email,
            ApiKey = apiKey,
            KeyLabel = label,
            ProtectedPassword = request.ProtectedPassword,
            CreatedAtUtc = now,
            ExpiresAtUtc = request.ExpiresAtUtc,
            IsActive = true,
            IsExpired = request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc.Value < now
        };
    }

    public async Task<IReadOnlyList<PlaygroundApiKeyDto>> ListAsync(
        Guid tenantId,
        string tenantConnectionString,
        string email,
        CancellationToken cancellationToken = default)
    {
        await EnsureTenantSchemaAsync(tenantId, tenantConnectionString, cancellationToken);
        var list = new List<PlaygroundApiKeyDto>();
        var now = DateTime.UtcNow;

        await using var conn = new SqlConnection(tenantConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            @"SELECT Id, Email, ApiKey, KeyLabel, ProtectedPassword, CreatedAtUtc, ExpiresAtUtc, IsActive
              FROM dbo.playgroundApiKeys
              WHERE IsActive = 1 AND Email = @email
              ORDER BY CreatedAtUtc DESC", conn);
        cmd.Parameters.AddWithValue("@email", email.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var expiresAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
            list.Add(new PlaygroundApiKeyDto
            {
                Id = reader.GetGuid(0),
                TenantId = tenantId,
                Email = reader.GetString(1),
                ApiKey = reader.GetString(2),
                KeyLabel = reader.IsDBNull(3) ? null : reader.GetString(3),
                ProtectedPassword = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAtUtc = reader.GetDateTime(5),
                ExpiresAtUtc = expiresAt,
                IsActive = reader.GetBoolean(7),
                IsExpired = expiresAt.HasValue && expiresAt.Value < now
            });
        }

        return list;
    }

    public async Task<PlaygroundApiKeyLookupDto?> LookupByApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogSchemaAsync(cancellationToken);
        var catalogCs = GetCatalogConnectionString();

        await using var conn = new SqlConnection(catalogCs);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            @"SELECT TOP 1 TenantId, KeyId, Email, ApiKey, IsActive
              FROM catalog.PlaygroundApiKeyRoutes
              WHERE ApiKey = @apiKey AND IsActive = 1", conn);
        cmd.Parameters.AddWithValue("@apiKey", apiKey.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new PlaygroundApiKeyLookupDto
        {
            TenantId = reader.GetGuid(0),
            KeyId = reader.GetGuid(1),
            Email = reader.GetString(2),
            ApiKey = reader.GetString(3),
            IsActive = reader.GetBoolean(4)
        };
    }

    public async Task<PlaygroundApiKeyDto?> GetByApiKeyAsync(
        Guid tenantId,
        string tenantConnectionString,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureTenantSchemaAsync(tenantId, tenantConnectionString, cancellationToken);
        var now = DateTime.UtcNow;

        await using var conn = new SqlConnection(tenantConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            @"SELECT TOP 1 Id, Email, ApiKey, KeyLabel, ProtectedPassword, CreatedAtUtc, ExpiresAtUtc, IsActive
              FROM dbo.playgroundApiKeys
              WHERE ApiKey = @apiKey AND IsActive = 1", conn);
        cmd.Parameters.AddWithValue("@apiKey", apiKey.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var expiresAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
        return new PlaygroundApiKeyDto
        {
            Id = reader.GetGuid(0),
            TenantId = tenantId,
            Email = reader.GetString(1),
            ApiKey = reader.GetString(2),
            KeyLabel = reader.IsDBNull(3) ? null : reader.GetString(3),
            ProtectedPassword = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAtUtc = reader.GetDateTime(5),
            ExpiresAtUtc = expiresAt,
            IsActive = reader.GetBoolean(7),
            IsExpired = expiresAt.HasValue && expiresAt.Value < now
        };
    }

    public async Task UpdateAccessTokenPasswordAsync(
        Guid tenantId,
        string tenantConnectionString,
        string apiKey,
        string? protectedPassword,
        CancellationToken cancellationToken = default)
    {
        await EnsureTenantSchemaAsync(tenantId, tenantConnectionString, cancellationToken);

        await using var conn = new SqlConnection(tenantConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            @"UPDATE dbo.playgroundApiKeys
              SET ProtectedPassword = @pwd
              WHERE ApiKey = @apiKey AND IsActive = 1", conn);
        cmd.Parameters.AddWithValue("@pwd", (object?)protectedPassword ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@apiKey", apiKey.Trim());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordUsageAsync(
        Guid tenantId,
        string tenantConnectionString,
        RecordPlaygroundApiUsageRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureTenantSchemaAsync(tenantId, tenantConnectionString, cancellationToken);
        var now = DateTime.UtcNow;

        await using var conn = new SqlConnection(tenantConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            @"INSERT INTO dbo.playgroundApiUsageLog
              (Id, ApiKeyId, ApiKey, Email, Endpoint, HttpMethod, StatusCode, DurationMs, RequestedAtUtc)
              VALUES (@id, @apiKeyId, @apiKey, @email, @endpoint, @method, @status, @duration, @requested)", conn);
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@apiKeyId", request.ApiKeyId);
        cmd.Parameters.AddWithValue("@apiKey", request.ApiKey.Trim());
        cmd.Parameters.AddWithValue("@email", request.Email.Trim());
        cmd.Parameters.AddWithValue("@endpoint", request.Endpoint.Length > 512 ? request.Endpoint[..512] : request.Endpoint);
        cmd.Parameters.AddWithValue("@method", request.HttpMethod.Trim());
        cmd.Parameters.AddWithValue("@status", request.StatusCode);
        cmd.Parameters.AddWithValue("@duration", request.DurationMs);
        cmd.Parameters.AddWithValue("@requested", now);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PlaygroundApiUsageSummaryDto> GetUsageAsync(
        Guid tenantId,
        string tenantConnectionString,
        string email,
        CancellationToken cancellationToken = default)
    {
        var keys = await ListAsync(tenantId, tenantConnectionString, email, cancellationToken);
        await EnsureTenantSchemaAsync(tenantId, tenantConnectionString, cancellationToken);

        var logs = new List<PlaygroundApiUsageLogDto>();
        var totalRequests = 0;
        var successfulRequests = 0;
        var failedRequests = 0;
        DateTime? lastUsedAtUtc = null;

        await using (var conn = new SqlConnection(tenantConnectionString))
        {
            await conn.OpenAsync(cancellationToken);
            await using (var countCmd = new SqlCommand(
                             @"SELECT
                                 COUNT(1) AS TotalRequests,
                                 SUM(CASE WHEN StatusCode >= 200 AND StatusCode < 300 THEN 1 ELSE 0 END) AS SuccessfulRequests,
                                 SUM(CASE WHEN StatusCode >= 400 THEN 1 ELSE 0 END) AS FailedRequests,
                                 MAX(RequestedAtUtc) AS LastUsedAtUtc
                               FROM dbo.playgroundApiUsageLog
                               WHERE Email = @email", conn))
            {
                countCmd.Parameters.AddWithValue("@email", email.Trim());
                await using var countReader = await countCmd.ExecuteReaderAsync(cancellationToken);
                if (await countReader.ReadAsync(cancellationToken))
                {
                    totalRequests = countReader.IsDBNull(0) ? 0 : countReader.GetInt32(0);
                    successfulRequests = countReader.IsDBNull(1) ? 0 : countReader.GetInt32(1);
                    failedRequests = countReader.IsDBNull(2) ? 0 : countReader.GetInt32(2);
                    lastUsedAtUtc = countReader.IsDBNull(3) ? null : countReader.GetDateTime(3);
                }
            }

            await using var cmd = new SqlCommand(
                @"SELECT TOP 500 Id, ApiKeyId, ApiKey, Email, Endpoint, HttpMethod, StatusCode, DurationMs, RequestedAtUtc
                  FROM dbo.playgroundApiUsageLog
                  WHERE Email = @email
                  ORDER BY RequestedAtUtc DESC", conn);
            cmd.Parameters.AddWithValue("@email", email.Trim());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                logs.Add(new PlaygroundApiUsageLogDto
                {
                    Id = reader.GetGuid(0),
                    ApiKeyId = reader.GetGuid(1),
                    ApiKey = reader.GetString(2),
                    Email = reader.GetString(3),
                    Endpoint = reader.GetString(4),
                    HttpMethod = reader.GetString(5),
                    StatusCode = reader.GetInt32(6),
                    DurationMs = reader.GetInt64(7),
                    RequestedAtUtc = reader.GetDateTime(8)
                });
            }
        }

        var activeKeys = keys.Count(k => !k.IsExpired);
        return new PlaygroundApiUsageSummaryDto
        {
            TenantId = tenantId,
            TotalKeys = keys.Count,
            ActiveKeys = activeKeys,
            ExpiredKeys = keys.Count - activeKeys,
            TotalRequests = totalRequests,
            SuccessfulRequests = successfulRequests,
            FailedRequests = failedRequests,
            LastUsedAtUtc = lastUsedAtUtc,
            RecentRequests = logs
        };
    }

    private async Task EnsureCatalogSchemaAsync(CancellationToken cancellationToken)
    {
        if (CatalogSchemaApplied.ContainsKey("catalog"))
            return;

        await CatalogSchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (CatalogSchemaApplied.ContainsKey("catalog"))
                return;

            await ApplySqlScriptAsync(GetCatalogConnectionString(), "CreatePlaygroundApiKeyCatalog.sql", cancellationToken);
            CatalogSchemaApplied["catalog"] = 1;
        }
        finally
        {
            CatalogSchemaLock.Release();
        }
    }

    private async Task EnsureTenantSchemaAsync(Guid tenantId, string connectionString, CancellationToken cancellationToken)
    {
        var key = tenantId.ToString();
        if (TenantSchemaApplied.ContainsKey(key))
            return;

        var gate = TenantSchemaLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TenantSchemaApplied.ContainsKey(key))
                return;

            await ApplySqlScriptAsync(connectionString, "CreatePlaygroundApiKeySchema.sql", cancellationToken);
            TenantSchemaApplied[key] = 1;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task ApplySqlScriptAsync(string connectionString, string scriptFileName, CancellationToken cancellationToken)
    {
        var script = await LoadScriptAsync(scriptFileName, cancellationToken);
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"(?m)^\s*GO\s*$")
            .Select(b => b.Trim())
            .Where(b => b.Length > 10)
            .ToList();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var batch in batches)
        {
            try
            {
                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex) when (ex.Number is 2714 or 1913 or 2705)
            {
                // idempotent
            }
        }
    }

    private async Task<string> LoadScriptAsync(string scriptFileName, CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", scriptFileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "scripts", scriptFileName)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", scriptFileName))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path, cancellationToken);
        }

        _logger.LogError("Playground SQL script not found: {Script}", scriptFileName);
        throw new FileNotFoundException($"Playground SQL script not found: {scriptFileName}");
    }

    private string GetCatalogConnectionString() =>
        _configuration.GetConnectionString("CatalogConnection")
        ?? _configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Catalog connection string is not configured.");
}
