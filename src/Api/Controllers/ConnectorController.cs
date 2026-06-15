using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Security;
using SaaSApp.Workflow.Application.Connectors;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Api.Controllers;

[ApiController]
[Route("api/connector")]
[Authorize(Policy = AuthorizationPolicies.TenantUser)]
public sealed class ConnectorController : ControllerBase
{
    private readonly IConnectorService _connectorService;

    public ConnectorController(IConnectorService connectorService) =>
        _connectorService = connectorService;

    /// <summary>Create a new connector (v5 POST /api/connector).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConnectorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] ConnectorUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _connectorService.CreateAsync(request, cancellationToken);
            return Created($"/api/connector/{created.Id}", created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Update connector by id (v5 PUT /api/connector/{id}).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ConnectorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ConnectorUpsertRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound(new { error = "ID mismatch" });

        try
        {
            var updated = await _connectorService.UpdateAsync(id, request, cancellationToken);
            if (updated == null)
                return NotFound(new { error = "Connector not found." });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get all active connectors (no body required).</summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(ConnectorListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var items = await _connectorService.ListAsync(new ConnectorListRequest(), cancellationToken);
            return Ok(new ConnectorListResponse(items));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>List connectors with filters (v5 POST /api/connector/all).</summary>
    [HttpPost("all")]
    [ProducesResponseType(typeof(ConnectorListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAll([FromBody] ConnectorListRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var items = await _connectorService.ListAsync(request, cancellationToken);
            return Ok(new ConnectorListResponse(items));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get connector by id (v5 GET /api/connector/{id}).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConnectorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        try
        {
            var item = await _connectorService.GetByIdAsync(id, cancellationToken);
            if (item == null)
                return NotFound();
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
