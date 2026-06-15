using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Security;

namespace SaaSApp.Api.Controllers;

/// <summary>STATIC document repositories (GUID, repository schema, paged items).</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TenantUser)]
public sealed class RepositoriesController : ControllerBase
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IStaticRepositoryProvisioner _provisioner;
    private readonly IRepositoryBrowseService _browse;
    private readonly IRepositoryItemQueryService _items;
    private readonly IRepositoryFileUploadService _fileUpload;
    private readonly IRepositoryArchiveFileUploadService _archiveUpload;
    private readonly IRepositoryStorageSeedService _storageSeed;
    private readonly IRepositoryItemActivityService _itemActivity;

    public RepositoriesController(
        ITenantProvider tenantProvider,
        IStaticRepositoryProvisioner provisioner,
        IRepositoryBrowseService browse,
        IRepositoryItemQueryService items,
        IRepositoryFileUploadService fileUpload,
        IRepositoryArchiveFileUploadService archiveUpload,
        IRepositoryStorageSeedService storageSeed,
        IRepositoryItemActivityService itemActivity)
    {
        _tenantProvider = tenantProvider;
        _provisioner = provisioner;
        _browse = browse;
        _items = items;
        _fileUpload = fileUpload;
        _archiveUpload = archiveUpload;
        _storageSeed = storageSeed;
        _itemActivity = itemActivity;
    }

    /// <summary>Seed default storage providers (EZOFIS, GCP, ONEDRIVE) for current tenant.</summary>
    [HttpPost("/api/repositories/storage-providers/seed")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<IActionResult> SeedStorageProviders(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        await _storageSeed.EnsureDefaultProvidersAsync(tenantId, GetUserId(), cancellationToken);
        var providers = await _storageSeed.ListProvidersAsync(tenantId, cancellationToken);
        return Ok(new { message = "Storage providers seeded.", providers });
    }

    /// <summary>List storage providers and their GUIDs for create-repository body.</summary>
    [HttpGet("/api/repositories/storage-providers")]
    public async Task<IActionResult> ListStorageProviders(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var providers = await _storageSeed.ListProvidersAsync(tenantId, cancellationToken);
        return Ok(providers);
    }

    [HttpPost("/api/repositories")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateRepositoryRequest body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "name is required." });

        var tenantId = RequireTenantId();
        try
        {
            var result = await _provisioner.CreateRepositoryAsync(body, tenantId, GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.RepositoryId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("/api/repositories")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var list = await _provisioner.ListRepositoriesAsync(tenantId, cancellationToken);
        return Ok(list);
    }

    [HttpGet("/api/repositories/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var repo = await _provisioner.GetRepositoryAsync(id, tenantId, cancellationToken);
        return repo == null ? NotFound() : Ok(repo);
    }

    /// <summary>Creates missing per-repo tables (e.g. stage table) for an existing repository.</summary>
    [HttpPost("/api/repositories/{id:guid}/provision-tables")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<IActionResult> ProvisionTables(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        try
        {
            await _provisioner.EnsureRepositoryTablesAsync(id, tenantId, cancellationToken);
            var repo = await _provisioner.GetRepositoryAsync(id, tenantId, cancellationToken);
            return Ok(new
            {
                message = "Repository tables verified.",
                itemsTableName = repo?.ItemsTableName,
                stageTableName = repo?.StageTableName
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Update repository name, storage, and/or field definitions (same field shape as create).</summary>
    [HttpPut("/api/repositories/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRepositoryRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        try
        {
            var repo = await _provisioner.UpdateRepositoryAsync(id, tenantId, request, GetUserId(), cancellationToken);
            return repo == null ? NotFound() : Ok(repo);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Folder fields and browse paths for this repository (driven by RepositoryFields, not hardcoded).</summary>
    [HttpGet("/api/repositories/{id:guid}/browse/structure")]
    public async Task<IActionResult> BrowseStructure(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        return await Browse(async () => await _browse.GetBrowseStructureAsync(id, tenantId, cancellationToken));
    }

    /// <summary>Next tree level. parentFilters JSON keys from GET .../browse/structure (not fixed field names).</summary>
    [HttpGet("/api/repositories/{id:guid}/browse/children")]
    public async Task<IActionResult> BrowseChildren(
        Guid id,
        [FromQuery] BrowseFolderQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var filters = ParseParentFilters(query.ParentFilters);
        return await Browse(async () =>
            await _browse.GetBrowseChildrenAsync(
                id, tenantId, query.PathId, filters, query.Page, query.PageSize, query.Search, cancellationToken));
    }

    /// <summary>Group items by any folder field name; parentFilters JSON for drill-down context.</summary>
    [HttpGet("/api/repositories/{id:guid}/browse/groups/{fieldName}")]
    public async Task<IActionResult> BrowseGroups(
        Guid id,
        string fieldName,
        [FromQuery] BrowseFolderQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var filters = ParseParentFilters(query.ParentFilters);
        return await Browse(async () =>
            await _browse.GetBrowseGroupsAsync(
                id, tenantId, fieldName, filters, query.Page, query.PageSize, query.Search, cancellationToken));
    }

    /// <summary>Allowed filter keys for GET .../items (per repository fields + standard columns).</summary>
    [HttpGet("/api/repositories/{id:guid}/items/filter-fields")]
    public async Task<IActionResult> GetItemFilterFields(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        try
        {
            var schema = await _items.GetItemListFilterSchemaAsync(id, tenantId, cancellationToken);
            return Ok(schema);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Bottom file list (paged). filters = JSON scope (same as browse parentFilters).
    /// Fast bulk: pageSize up to 500, skipTotal=true, cursor from response nextCursor for infinite scroll.
    /// </summary>
    [HttpGet("/api/repositories/{id:guid}/items")]
    public async Task<IActionResult> ListItems(Guid id, [FromQuery] RepositoryItemListQuery query, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        try
        {
            var result = await _items.ListItemsAsync(id, tenantId, query, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("/api/repositories/{id:guid}/items/facets/{fieldName}")]
    public async Task<IActionResult> Facets(
        Guid id,
        string fieldName,
        [FromQuery] string? scopeFilters = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        try
        {
            var facets = await _items.GetFacetsAsync(id, tenantId, fieldName, scopeFilters, limit, cancellationToken);
            return Ok(facets);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("/api/repositories/{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> GetItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var item = await _items.GetItemAsync(id, tenantId, itemId, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>Structured document workspace (panels + line items) for filename click detail view.</summary>
    [HttpGet("/api/repositories/{id:guid}/items/{itemId:guid}/workspace")]
    public async Task<IActionResult> GetItemWorkspace(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var workspace = await _items.GetItemWorkspaceAsync(id, tenantId, itemId, cancellationToken);
        return workspace == null ? NotFound() : Ok(workspace);
    }

    [HttpGet("/api/repositories/{id:guid}/items/{itemId:guid}/timeline")]
    public async Task<IActionResult> GetItemTimeline(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var timeline = await _itemActivity.GetTimelineAsync(id, tenantId, itemId, cancellationToken);
        return timeline == null ? NotFound() : Ok(timeline);
    }

    [HttpPost("/api/repositories/{id:guid}/items/{itemId:guid}/timeline")]
    public async Task<IActionResult> AddItemTimelineEvent(
        Guid id,
        Guid itemId,
        [FromBody] AddRepositoryItemTimelineEventRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        try
        {
            var evt = await _itemActivity.AddTimelineEventAsync(id, tenantId, itemId, request, GetUserId(), cancellationToken);
            return evt == null ? NotFound() : Ok(evt);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("/api/repositories/{id:guid}/items/{itemId:guid}/comments")]
    public async Task<IActionResult> GetItemComments(
        Guid id,
        Guid itemId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var comments = await _itemActivity.GetCommentsAsync(id, tenantId, itemId, page, pageSize, cancellationToken);
        return comments == null ? NotFound() : Ok(comments);
    }

    [HttpPost("/api/repositories/{id:guid}/items/{itemId:guid}/comments")]
    public async Task<IActionResult> AddItemComment(
        Guid id,
        Guid itemId,
        [FromBody] AddRepositoryItemCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "User id is required to post a comment." });

        var tenantId = RequireTenantId();
        try
        {
            var result = await _itemActivity.AddCommentAsync(id, tenantId, itemId, request, userId.Value, cancellationToken);
            if (result == null)
                return NotFound();

            return Created($"/api/repositories/{id:D}/items/{itemId:D}/comments/{result.CommentId:D}", result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("/api/repositories/{id:guid}/items")]
    public async Task<IActionResult> CreateItem(Guid id, [FromBody] CreateRepositoryItemRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var itemId = await _items.CreateItemAsync(id, tenantId, request, GetUserId(), cancellationToken);
        return CreatedAtAction(nameof(GetItem), new { id, itemId }, new { itemId });
    }

    /// <summary>Update metadata on an existing item (after upload). Body = JSON object, same keys as upload metadata.</summary>
    [HttpPatch("/api/repositories/{id:guid}/items/{itemId:guid}/metadata")]
    public async Task<IActionResult> UpdateItemMetadata(
        Guid id,
        Guid itemId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        try
        {
            var metadata = ParseMetadataBody(body);
            if (metadata.Count == 0)
                return BadRequest(new { error = "metadata JSON object with at least one field is required." });

            var result = await _items.UpdateItemMetadataAsync(id, tenantId, itemId, metadata, GetUserId(), cancellationToken);
            if (result == null)
                return NotFound();

            var item = await _items.GetItemAsync(id, tenantId, itemId, cancellationToken);
            return Ok(new
            {
                result.ItemId,
                result.UpdatedFieldCount,
                item
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Upload with archive folder layout (multipart): ezts{tenantId}/archive/{repositoryName}/{level names}/{file}.
    /// Requires repository fields with IncludeInFolderStructure; metadata JSON required for mandatory levels.
    /// </summary>
    [HttpPost("/api/repositories/{id:guid}/items/upload-archive")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    public async Task<IActionResult> UploadItemArchive(
        Guid id,
        IFormFile? file,
        [FromForm] Guid? workflowId,
        [FromForm] int? processId,
        [FromForm] Guid? instanceId,
        [FromForm] int? transactionId,
        [FromForm] string? storageProviderCode,
        [FromForm] string? metadata,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "file is required." });

        var tenantId = RequireTenantId();
        try
        {
            var mergedMetadata = RepositoryFormMetadataCollector.ToMetadataJson(
                RepositoryFormMetadataCollector.Collect(metadata, EnumerateExtraFormFields()));

            await using var stream = file.OpenReadStream();
            var request = new RepositoryUploadItemRequest(
                stream,
                file.FileName,
                file.ContentType,
                workflowId,
                processId,
                instanceId,
                transactionId,
                storageProviderCode,
                file.Length,
                mergedMetadata);

            var result = await _archiveUpload.UploadItemAsync(id, tenantId, request, GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetItem), new { id, itemId = result.ItemId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Upload a file into the repository (multipart, flat path). Optionally link to workflow via workflowId + processId and/or instanceId.</summary>
    [HttpPost("/api/repositories/{id:guid}/items/upload")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    public async Task<IActionResult> UploadItem(
        Guid id,
        IFormFile? file,
        [FromForm] Guid? workflowId,
        [FromForm] int? processId,
        [FromForm] Guid? instanceId,
        [FromForm] int? transactionId,
        [FromForm] string? storageProviderCode,
        [FromForm] string? metadata,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "file is required." });

        var tenantId = RequireTenantId();
        try
        {
            var mergedMetadata = RepositoryFormMetadataCollector.ToMetadataJson(
                RepositoryFormMetadataCollector.Collect(metadata, EnumerateExtraFormFields()));

            await using var stream = file.OpenReadStream();
            var request = new RepositoryUploadItemRequest(
                stream,
                file.FileName,
                file.ContentType,
                workflowId,
                processId,
                instanceId,
                transactionId,
                storageProviderCode,
                file.Length,
                mergedMetadata);

            var result = await _fileUpload.UploadItemAsync(id, tenantId, request, GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetItem), new { id, itemId = result.ItemId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Download or inline-view the item file from storage (EZOFIS blob or local fallback).</summary>
    [HttpGet("/api/repositories/{id:guid}/items/{itemId:guid}/file")]
    public async Task<IActionResult> GetItemFile(Guid id, Guid itemId, [FromQuery] string disposition = "inline", CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        try
        {
            var content = await _items.OpenItemFileAsync(id, tenantId, itemId, cancellationToken);
            if (content == null)
                return NotFound();

            var inline = string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase);
            return new FileStreamResult(content.Stream, content.ContentType)
            {
                FileDownloadName = inline ? null : content.FileName,
                EnableRangeProcessing = true
            };
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static IReadOnlyDictionary<string, string> ParseMetadataBody(JsonElement body)
    {
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("metadata", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            return RepositoryMetadataParser.Parse(nested.GetRawText());
        }

        if (body.ValueKind == JsonValueKind.Object)
            return RepositoryMetadataParser.Parse(body.GetRawText());

        throw new ArgumentException("Request body must be a JSON object, e.g. {\"Supplier\":\"Acme\",\"InvoiceNumber\":\"INV-1\"}.");
    }

    private static IReadOnlyDictionary<string, string> ParseParentFilters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict == null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return dict
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value.Trim(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"parentFilters must be a JSON object, e.g. {{\"Supplier\":\"Acme Supplies\"}}. {ex.Message}");
        }
    }

    private IEnumerable<KeyValuePair<string, string?>> EnumerateExtraFormFields()
    {
        if (!Request.HasFormContentType)
            yield break;

        foreach (var key in Request.Form.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            yield return new KeyValuePair<string, string?>(key, Request.Form[key].ToString());
        }
    }

    private async Task<IActionResult> Browse<T>(Func<Task<T>> task)
    {
        try
        {
            return Ok(await task());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private Guid RequireTenantId() =>
        _tenantProvider.GetTenantId() ?? throw new InvalidOperationException("Tenant context is required (X-Tenant-Id).");

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("oid");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
