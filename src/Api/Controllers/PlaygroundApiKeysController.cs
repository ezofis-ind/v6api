using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Api.Services;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Controllers;

/// <summary>
/// Playground API key and usage storage in the tenant database.
/// Called by the external V6 Playground host after login/tenant resolution.
/// </summary>
[ApiController]
[Route("api/playground")]
[AllowAnonymous]
public sealed class PlaygroundApiKeysController : ControllerBase
{
    private readonly IPlaygroundApiKeyService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly ITenantConnectionStringResolver _connectionResolver;
    private readonly ILogger<PlaygroundApiKeysController> _logger;

    public PlaygroundApiKeysController(
        IPlaygroundApiKeyService service,
        ITenantProvider tenantProvider,
        ITenantConnectionProvider connectionProvider,
        ITenantConnectionStringResolver connectionResolver,
        ILogger<PlaygroundApiKeysController> logger)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _connectionProvider = connectionProvider;
        _connectionResolver = connectionResolver;
        _logger = logger;
    }

    /// <summary>Create and store a playground API key for the current tenant.</summary>
    [HttpPost("api-keys")]
    public async Task<IActionResult> CreateApiKey(
        [FromBody] CreatePlaygroundApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(cancellationToken);
        if (tenant.Error is not null)
            return tenant.Error;

        var created = await _service.CreateAsync(tenant.TenantId!.Value, tenant.ConnectionString!, request, cancellationToken);
        return Ok(created);
    }

    /// <summary>List playground API keys for an email in the current tenant.</summary>
    [HttpGet("api-keys")]
    public async Task<IActionResult> ListApiKeys(
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "email is required." });

        var tenant = await ResolveTenantAsync(cancellationToken);
        if (tenant.Error is not null)
            return tenant.Error;

        var keys = await _service.ListAsync(tenant.TenantId!.Value, tenant.ConnectionString!, email, cancellationToken);
        return Ok(new { totalKeys = keys.Count, keys });
    }

    /// <summary>Resolve tenant from an API key (catalog route table).</summary>
    [HttpGet("api-keys/lookup")]
    public async Task<IActionResult> LookupApiKey(
        [FromQuery] string apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest(new { error = "apiKey is required." });

        var lookup = await _service.LookupByApiKeyAsync(apiKey, cancellationToken);
        if (lookup is null)
            return NotFound(new { error = "API key not found." });

        return Ok(lookup);
    }

    /// <summary>Get full key details from tenant DB after lookup.</summary>
    [HttpGet("api-keys/by-key")]
    public async Task<IActionResult> GetApiKeyByValue(
        [FromQuery] string apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest(new { error = "apiKey is required." });

        var lookup = await _service.LookupByApiKeyAsync(apiKey, cancellationToken);
        if (lookup is null)
            return NotFound(new { error = "API key not found." });

        var conn = await _connectionResolver.GetConnectionStringAsync(lookup.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(conn))
            return StatusCode(500, new { error = "Tenant connection not found." });

        var record = await _service.GetByApiKeyAsync(lookup.TenantId, conn, apiKey, cancellationToken);
        if (record is null)
            return NotFound(new { error = "API key not found in tenant database." });

        if (record.IsExpired)
            return Unauthorized(new { message = "API Key has expired." });

        return Ok(record);
    }

    /// <summary>Insert a playground API usage log row for the current tenant.</summary>
    [HttpPost("api-usage")]
    public async Task<IActionResult> RecordUsage(
        [FromBody] RecordPlaygroundApiUsageRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(cancellationToken);
        if (tenant.Error is not null)
            return tenant.Error;

        await _service.RecordUsageAsync(tenant.TenantId!.Value, tenant.ConnectionString!, request, cancellationToken);
        return Ok(new { message = "Usage recorded." });
    }

    /// <summary>Get usage summary for an email in the current tenant.</summary>
    [HttpGet("api-usage")]
    public async Task<IActionResult> GetUsage(
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "email is required." });

        var tenant = await ResolveTenantAsync(cancellationToken);
        if (tenant.Error is not null)
            return tenant.Error;

        var summary = await _service.GetUsageAsync(tenant.TenantId!.Value, tenant.ConnectionString!, email, cancellationToken);
        return Ok(summary);
    }

    private async Task<(Guid? TenantId, string? ConnectionString, IActionResult? Error)> ResolveTenantAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId is null || tenantId.Value == Guid.Empty)
            return (null, null, BadRequest(new { error = "X-Tenant-Id header is required." }));

        var connectionString = _connectionProvider.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = await _connectionResolver.GetConnectionStringAsync(tenantId.Value, cancellationToken);
            if (!string.IsNullOrWhiteSpace(connectionString))
                _connectionProvider.SetConnectionString(connectionString);
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Tenant connection not resolved for playground request. TenantId={TenantId}", tenantId);
            return (null, null, BadRequest(new { error = "Tenant database connection could not be resolved." }));
        }

        return (tenantId, connectionString, null);
    }
}
