using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Security;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Api.Controllers;

/// <summary>Proxy to Python field-mapping (excel columns ↔ form fields).</summary>
[ApiController]
[Route("api/field-mapping")]
[Authorize(Policy = AuthorizationPolicies.TenantUser)]
public sealed class FieldMappingController : ControllerBase
{
    private readonly IFieldMappingService _fieldMapping;

    public FieldMappingController(IFieldMappingService fieldMapping) =>
        _fieldMapping = fieldMapping;

    /// <summary>
    /// Map Excel sheet columns to form header / line-item fields via Python
    /// (<c>http://52.172.32.88:8095/api/v1/field-mapping</c>).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> MapFields(
        [FromBody] FieldMappingRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required." });

        try
        {
            var result = await _fieldMapping.MapFieldsAsync(request, cancellationToken);
            return Content(result.GetRawText(), "application/json");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Field-mapping service unreachable: {ex.Message}" });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { error = "Field-mapping service timed out." });
        }
    }
}
