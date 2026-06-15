using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Api.Services;
using SaaSApp.Dms.Domain.Models;
using SaaSApp.Dms.Infrastructure.Services;
using SaaSApp.MultiTenancy;
using SaaSApp.Security;

namespace SaaSApp.Api.Controllers;

/// <summary>DMS (Document Management System) - folder explorer and document listing.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.TenantUser)]
public sealed class DmsController : ControllerBase
{
    private readonly IDmsFolderService _folderService;
    private readonly IDmsSchemaService _schemaService;
    private readonly ITenantConnectionProvider _connectionProvider;

    public DmsController(IDmsFolderService folderService, IDmsSchemaService schemaService, ITenantConnectionProvider connectionProvider)
    {
        _folderService = folderService;
        _schemaService = schemaService;
        _connectionProvider = connectionProvider;
    }

    /// <summary>Apply DMS schema to current tenant database. Call if dms.Repository is missing. Admin only.</summary>
    [HttpPost("setup-schema")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetupSchema(CancellationToken cancellationToken)
    {
        var conn = _connectionProvider.ConnectionString;
        if (string.IsNullOrEmpty(conn))
            return BadRequest("Tenant connection not set. Provide X-Tenant-Id.");
        await _schemaService.ApplySchemaAsync(conn, cancellationToken);
        return Ok(new { message = "DMS schema applied successfully." });
    }

    /// <summary>Get folder children for file explorer tree. Path: "" (root), "2025", "2025/Purchase", "2025/Purchase/Acme Corp".</summary>
    [HttpGet("repositories/{repositoryId:guid}/folders/children")]
    [ProducesResponseType(typeof(FolderChildrenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFolderChildren(
        Guid repositoryId,
        [FromQuery] string path = "",
        [FromQuery] string tableName = "sample_items",
        CancellationToken cancellationToken = default)
    {
        var result = await _folderService.GetFolderChildrenAsync(repositoryId, tableName, path ?? "", cancellationToken);
        return Ok(result);
    }

    /// <summary>Get documents in folder. Path must be full: "2025/Purchase/Acme Corp".</summary>
    [HttpGet("repositories/{repositoryId:guid}/documents")]
    [ProducesResponseType(typeof(DocumentListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocumentsInFolder(
        Guid repositoryId,
        [FromQuery] string path,
        [FromQuery] string tableName = "sample_items",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _folderService.GetDocumentsInFolderAsync(repositoryId, tableName, path ?? "", page, pageSize, cancellationToken);
        return Ok(result);
    }
}
