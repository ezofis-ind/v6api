using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.ActivityLog.Application.Contracts;
using SaaSApp.MultiTenancy;
using SaaSApp.Security;

namespace SaaSApp.Api.Controllers;

/// <summary>Admin-only Event Log list for the current tenant (UI event timeline).</summary>
[ApiController]
[Route("api/event-logs")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class EventLogsController : ControllerBase
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IEventLogQueryService _queryService;

    public EventLogsController(ITenantProvider tenantProvider, IEventLogQueryService queryService)
    {
        _tenantProvider = tenantProvider;
        _queryService = queryService;
    }

    /// <summary>Paginated event log rows shaped for the Event Log grid.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EventLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EventLogEntryDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? userEmail = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null)
            return BadRequest(new { error = "Tenant not resolved." });

        var query = new ListEventLogsQuery(
            page, pageSize, category, severity, userEmail, dateFrom, dateTo, search);
        var result = await _queryService.ListAsync(tenantId.Value, query, cancellationToken);
        return Ok(result);
    }
}
