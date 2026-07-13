using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SaaSApp.ActivityLog.Application.Contracts;
using SaaSApp.ActivityLog.Infrastructure.Options;
using SaaSApp.MultiTenancy;
using SaaSApp.Security;

namespace SaaSApp.Api.Controllers;

/// <summary>Admin-only API access log search for the current tenant. Disabled while ActivityLog:Enabled is false.</summary>
[ApiController]
[Route("api/activity-logs")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ActivityLogsController : ControllerBase
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IActivityLogQueryService _queryService;
    private readonly ActivityLogOptions _options;

    public ActivityLogsController(
        ITenantProvider tenantProvider,
        IActivityLogQueryService queryService,
        IOptions<ActivityLogOptions> options)
    {
        _tenantProvider = tenantProvider;
        _queryService = queryService;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ActivityLogEntryDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? method = null,
        [FromQuery] string? path = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return NotFound();

        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null)
            return BadRequest(new { error = "Tenant not resolved." });

        var query = new ListActivityLogsQuery(
            page, pageSize, userId, method, path, statusCode, dateFrom, dateTo, correlationId);
        var result = await _queryService.ListAsync(tenantId.Value, query, cancellationToken);
        return Ok(result);
    }
}
