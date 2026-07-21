namespace SaaSApp.Api.Services;

public interface IPlaygroundApiKeyService
{
    Task<PlaygroundApiKeyDto> CreateAsync(
        Guid tenantId,
        string tenantConnectionString,
        CreatePlaygroundApiKeyRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaygroundApiKeyDto>> ListAsync(
        Guid tenantId,
        string tenantConnectionString,
        string email,
        CancellationToken cancellationToken = default);

    Task<PlaygroundApiKeyLookupDto?> LookupByApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<PlaygroundApiKeyDto?> GetByApiKeyAsync(
        Guid tenantId,
        string tenantConnectionString,
        string apiKey,
        CancellationToken cancellationToken = default);

    Task UpdateAccessTokenPasswordAsync(
        Guid tenantId,
        string tenantConnectionString,
        string apiKey,
        string? protectedPassword,
        CancellationToken cancellationToken = default);

    Task RecordUsageAsync(
        Guid tenantId,
        string tenantConnectionString,
        RecordPlaygroundApiUsageRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaygroundApiUsageSummaryDto> GetUsageAsync(
        Guid tenantId,
        string tenantConnectionString,
        string email,
        CancellationToken cancellationToken = default);
}

public sealed record CreatePlaygroundApiKeyRequest(
    string Email,
    string ApiKey,
    string? KeyLabel,
    string? ProtectedPassword,
    DateTime? ExpiresAtUtc);

public sealed record RecordPlaygroundApiUsageRequest(
    Guid ApiKeyId,
    string ApiKey,
    string Email,
    string Endpoint,
    string HttpMethod,
    int StatusCode,
    long DurationMs);

public sealed class PlaygroundApiKeyDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string? KeyLabel { get; set; }
    public string? ProtectedPassword { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
}

public sealed class PlaygroundApiKeyLookupDto
{
    public Guid TenantId { get; set; }
    public Guid KeyId { get; set; }
    public string Email { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class PlaygroundApiUsageLogDto
{
    public Guid Id { get; set; }
    public Guid ApiKeyId { get; set; }
    public string ApiKey { get; set; } = "";
    public string Email { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string HttpMethod { get; set; } = "";
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public DateTime RequestedAtUtc { get; set; }
}

public sealed class PlaygroundApiUsageSummaryDto
{
    public Guid TenantId { get; set; }
    public int TotalKeys { get; set; }
    public int ActiveKeys { get; set; }
    public int ExpiredKeys { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public IReadOnlyList<PlaygroundApiUsageLogDto> RecentRequests { get; set; } = Array.Empty<PlaygroundApiUsageLogDto>();
}
