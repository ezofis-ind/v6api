using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Api.Helpers;
using SaaSApp.Security;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Forms;

namespace SaaSApp.Api.Controllers;

/// <summary>v5-compatible form designer API (create, list all, get by id with formJson).</summary>
[ApiController]
[Route("api/form")]
[Authorize(Policy = AuthorizationPolicies.TenantUser)]
public sealed class FormController : ControllerBase
{
    private readonly IFormService _formService;

    public FormController(IFormService formService) =>
        _formService = formService;

    /// <summary>Add new form from designer JSON (v5 POST /api/form).</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostForm([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (body.ValueKind != JsonValueKind.Object)
            return BadRequest(new { error = "Body must be a JSON object." });

        var designerRaw = FormJsonBodyHelper.ExtractDesignerJsonRaw(body);
        if (string.IsNullOrWhiteSpace(designerRaw))
            return BadRequest(new { error = "Invalid form JSON. Send designer payload with settings/panels or wrap in formJson." });

        FormJsonDto formJson;
        try
        {
            using var document = JsonDocument.Parse(designerRaw);
            formJson = FormJsonBodyHelper.NormalizeForCreate(document.RootElement);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Invalid form JSON." });
        }

        try
        {
            var result = await _formService.CreateFormAsync(formJson, designerRaw, cancellationToken);

            return result.Status switch
            {
                FormCreateStatus.Created when !string.IsNullOrWhiteSpace(result.FormId) =>
                    Created($"/api/form/{result.FormId}", result.FormId),
                FormCreateStatus.NameConflict =>
                    StatusCode(StatusCodes.Status406NotAcceptable, result.Message ?? "Not Acceptable"),
                _ => NotFound(result.Message ?? "Not Found")
            };
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>List forms for the current tenant (formId and formName only).</summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(FormListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListForms(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _formService.ListAsync(cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Forms with filter, sort, group, pagination (same shape as POST /api/workflow/all).</summary>
    [HttpPost("all")]
    [ProducesResponseType(typeof(FormAllResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryForms([FromBody] FormAllRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _formService.QueryAllAsync(
                request,
                GetCurrentUserId(),
                IsCurrentUserAdmin(),
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Update an existing form from designer JSON (v5 PUT /api/form/{id}).</summary>
    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutForm(string id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        if (body.ValueKind != JsonValueKind.Object)
            return BadRequest(new { error = "Body must be a JSON object." });

        var designerRaw = FormJsonBodyHelper.ExtractDesignerJsonRaw(body);
        if (string.IsNullOrWhiteSpace(designerRaw))
            return BadRequest(new { error = "Invalid form JSON. Send designer payload with settings/panels or wrap in formJson." });

        FormJsonDto formJson;
        try
        {
            using var document = JsonDocument.Parse(designerRaw);
            formJson = FormJsonBodyHelper.NormalizeForCreate(document.RootElement);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Invalid form JSON." });
        }

        try
        {
            var result = await _formService.UpdateFormAsync(id, formJson, designerRaw, cancellationToken);

            return result.Status switch
            {
                FormUpdateStatus.Updated when !string.IsNullOrWhiteSpace(result.FormId) =>
                    Ok(result.FormId),
                FormUpdateStatus.NameConflict =>
                    StatusCode(StatusCodes.Status406NotAcceptable, result.Message ?? "Not Acceptable"),
                _ => NotFound(result.Message ?? "Not Found")
            };
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Soft-delete a form by id.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteForm(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        try
        {
            var result = await _formService.DeleteFormAsync(id, cancellationToken);
            return result.Status switch
            {
                FormDeleteStatus.Deleted => NoContent(),
                _ => NotFound(result.Message ?? "Not Found")
            };
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get a form by id with designer JSON (<c>formJson</c>) from blob/file storage.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FormByIdResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _formService.GetByIdAsync(id, cancellationToken);
            if (result == null)
                return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private bool IsCurrentUserAdmin() =>
        User.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role") &&
            (string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(c.Value, "Administrator", StringComparison.OrdinalIgnoreCase)));
}
