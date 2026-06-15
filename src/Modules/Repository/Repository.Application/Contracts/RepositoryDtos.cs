using System.Text.Json.Serialization;

namespace SaaSApp.Repository.Application.Contracts;

public sealed record CreateRepositoryRequest(
    string Name,
    string? Description = null,
    Guid? StorageProviderId = null,
    /// <summary>Alternative to StorageProviderId — e.g. EZOFIS, GCP, ONEDRIVE (case-insensitive).</summary>
    string? StorageProviderCode = null,
    string? StorageDrive = null,
    IReadOnlyList<RepositoryFieldDefinitionDto>? Fields = null);

public sealed record StorageProviderDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record UpdateRepositoryRequest(
    string? Name = null,
    string? Description = null,
    Guid? StorageProviderId = null,
    string? StorageProviderCode = null,
    string? StorageDrive = null,
    /// <summary>When set, replaces active field definitions (include <c>id</c> to update; omit <c>id</c> to add).</summary>
    IReadOnlyList<RepositoryFieldDefinitionDto>? Fields = null);

public sealed record RepositoryFieldDefinitionDto(
    string Name,
    string? DataType,
    int Level = 0,
    bool IsMandatory = false,
    bool IncludeInFolderStructure = false,
    string? OptionsJson = null,
    int? OrderId = null,
    bool IsReadOnly = false,
    /// <summary>Set when editing an existing field; omit for new fields.</summary>
    Guid? Id = null);

public sealed record CreateRepositoryResult(Guid RepositoryId, string ItemsTableName, string StageTableName);

public sealed record RepositorySummaryDto(
    Guid Id,
    string Name,
    string? Description,
    Guid StorageProviderId,
    string ItemsTableName,
    DateTime CreatedAtUtc,
    Guid? CreatedBy = null,
    Guid? ModifiedBy = null,
    string? CreatedByName = null,
    string? ModifiedByName = null);

public sealed record RepositoryDetailDto(
    Guid Id,
    string Name,
    string? Description,
    Guid StorageProviderId,
    string? StorageDrive,
    string ItemsTableName,
    string StageTableName,
    IReadOnlyList<RepositoryFieldDto> Fields);

public sealed record RepositoryFieldDto(
    Guid Id,
    string Name,
    string SqlColumnName,
    string? DataType,
    int Level,
    bool IsMandatory,
    bool IncludeInFolderStructure,
    string? OptionsJson = null,
    int? OrderId = null,
    bool IsReadOnly = false);

public sealed record BrowseGroupDto(string Name, int ItemCount, DateTime? DateModified);

/// <summary>Next tree level for folder UI (e.g. Supplier → DocumentType folders under Acme).</summary>
public sealed record BrowseChildrenResponseDto(
    int Level,
    string GroupField,
    string GroupFieldName,
    string PathId,
    string PathLabel,
    IReadOnlyDictionary<string, string> ParentFilters,
    bool IsLeafLevel,
    PagedResult<BrowseGroupDto> Groups);

/// <summary>Folder fields for this repository (from RepositoryFields.IncludeInFolderStructure).</summary>
public sealed record BrowseStructureDto(
    IReadOnlyList<BrowseFolderFieldDto> FolderFields,
    IReadOnlyList<BrowsePathDto> BrowsePaths);

public sealed record BrowseFolderFieldDto(
    int Level,
    string Name,
    string SqlColumnName);

/// <summary>One way to drill down (e.g. Supplier → DocumentType vs DocumentType → Supplier).</summary>
public sealed record BrowsePathDto(string Id, string Label, IReadOnlyList<string> FieldOrder);

/// <summary>
/// Browse query. Parent filters are dynamic JSON — keys come from GET .../browse/structure (folderFields.sqlColumnName).
/// </summary>
public sealed class BrowseFolderQuery
{
    /// <summary>Browse path from GET /browse/structure (e.g. by-Supplier).</summary>
    public string PathId { get; init; } = "by-Supplier";

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public string? Search { get; init; }

    /// <summary>
    /// JSON object of parent folder filters. Not hardcoded to any repo.
    /// Step 1: omit or {}. Step 2 (example): {"Supplier":"Acme Supplies"}.
    /// Keys = sqlColumnName from browse/structure; values = selected group name from previous level.
    /// </summary>
    public string? ParentFilters { get; init; }
}

/// <summary>
/// Paged item list. Scope filters are dynamic JSON (keys = sqlColumnName from GET .../items/filter-fields).
/// Same JSON as browse parentFilters, e.g. {"Supplier":"IndiaLogistics"}.
/// </summary>
public sealed class RepositoryItemListQuery
{
    public string? Filters { get; init; }

    public string? Search { get; init; }

    public DateTime? DateFrom { get; init; }

    public DateTime? DateTo { get; init; }

    public string SortBy { get; init; } = "documentDate";

    public string SortOrder { get; init; } = "desc";

    /// <summary>1-based page when not using cursor. Ignored when <see cref="Cursor"/> is set.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Rows per page (1–500). Use 100–200 for grid; combine with cursor for infinite scroll.</summary>
    public int PageSize { get; init; } = 50;

    /// <summary>Skip COUNT(*) for faster loads (infinite scroll). Response <see cref="PagedResult{T}.TotalCount"/> is -1.</summary>
    public bool SkipTotal { get; init; }

    /// <summary>Keyset token from previous response <see cref="PagedResult{T}.NextCursor"/> (fast on 10L+ rows).</summary>
    public string? Cursor { get; init; }
}

public sealed record ItemListFilterFieldDto(string Name, string SqlColumnName, string? DataType);

public sealed record ItemListFilterSchemaDto(IReadOnlyList<ItemListFilterFieldDto> Fields);

public sealed record RepositoryItemListDto(
    Guid Id,
    string? FileName,
    int? FileVersion,
    string? DocumentType,
    string? Supplier,
    string? InvoiceNumber,
    string? PoNumber,
    DateTime? DocumentDate,
    decimal? Amount,
    string? Currency,
    string? Status,
    byte? OcrPercent,
    string? AiStatus,
    string? RiskLevel,
    string? Source,
    string? Department,
    Guid StorageProviderId,
    string? StorageProviderCode,
    bool HasFilePath);

public sealed record RepositoryItemDetailDto(
    Guid Id,
    string? FileName,
    string? FilePath,
    string? FileType,
    int? FileSize,
    Guid StorageProviderId,
    string? StorageProviderCode,
    IReadOnlyDictionary<string, object?> Fields);

/// <summary>Label/value pair for document detail side panels.</summary>
public sealed record RepositoryItemPanelFieldDto(string Key, string Label, string? Value);

public sealed record RepositoryItemPanelSectionDto(
    string SectionKey,
    string Title,
    IReadOnlyList<RepositoryItemPanelFieldDto> Fields);

public sealed record RepositoryItemLineItemRowDto(
    string? Description,
    decimal? Qty,
    decimal? UnitPrice,
    decimal? Gst,
    decimal? Total);

public sealed record RepositoryItemLineItemsSectionDto(
    IReadOnlyList<RepositoryItemLineItemRowDto> Rows,
    decimal? GrandTotal,
    string? Currency);

/// <summary>
/// Structured item detail for the document workspace UI (click filename).
/// Side panels are in <see cref="DetailsRow"/> (document, supplier, AI, system).
/// </summary>
public sealed record RepositoryItemWorkspaceDto(
    Guid Id,
    string? FileName,
    string? FileType,
    int? FileSize,
    string FileUrl,
    Guid StorageProviderId,
    string? StorageProviderCode,
    [property: JsonPropertyName("DetailsRow")] IReadOnlyList<RepositoryItemPanelSectionDto> DetailsRow,
    RepositoryItemLineItemsSectionDto? LineItems = null);

public sealed record CreateRepositoryItemRequest(
    Guid? StorageProviderId,
    string? FilePath,
    string? FileName,
    string? FileType,
    int? FileSize,
    string? DocumentType,
    string? Supplier,
    string? InvoiceNumber,
    string? PoNumber,
    DateTime? DocumentDate,
    decimal? Amount,
    string? Currency,
    string? Status,
    byte? OcrPercent,
    string? AiStatus,
    string? RiskLevel,
    string? Source,
    string? Department,
    Guid? FolderId,
    Guid? WorkflowInstanceId = null,
    IReadOnlyDictionary<string, string>? FieldValues = null,
    int? FileVersion = null);

/// <summary>Multipart upload: file + optional workflow link (workflowId + processId and/or instanceId).</summary>
public sealed record RepositoryUploadItemRequest(
    Stream FileStream,
    string FileName,
    string? ContentType,
    Guid? WorkflowId = null,
    int? ProcessId = null,
    Guid? InstanceId = null,
    int? TransactionId = null,
    string? StorageProviderCode = null,
    long? FileSize = null,
    string? Metadata = null);

public sealed record RepositoryUploadItemResult(
    Guid ItemId,
    string FileName,
    string FilePath,
    string StorageProviderCode,
    int FileVersion,
    bool WorkflowAttached,
    Guid? WorkflowId,
    int? ProcessId,
    Guid? InstanceId);

/// <summary>JSON body for PATCH .../items/{itemId}/metadata — same keys as upload metadata.</summary>
public sealed record UpdateRepositoryItemMetadataRequest(
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record UpdateRepositoryItemMetadataResult(
    Guid ItemId,
    int UpdatedFieldCount);

public sealed record RepositoryItemFileContent(
    Stream Stream,
    string FileName,
    string ContentType,
    long? Length);

public sealed record RepositoryItemTimelineEventDto(
    Guid Id,
    string EventType,
    string Title,
    string? Description,
    string? ActorType,
    string? ActorName,
    DateTime CreatedAtUtc,
    bool IsDerived = false);

public sealed record RepositoryItemTimelineResultDto(
    IReadOnlyList<RepositoryItemTimelineEventDto> Events,
    int TotalCount);

public sealed record AddRepositoryItemTimelineEventRequest(
    string Title,
    string? Description = null,
    string? EventType = "user",
    string? ActorName = null);

public sealed record RepositoryItemCommentDto(
    Guid Id,
    string Body,
    Guid AuthorUserId,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record RepositoryItemCommentsResultDto(
    IReadOnlyList<RepositoryItemCommentDto> Comments,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AddRepositoryItemCommentRequest(string Body);

public sealed record AddRepositoryItemCommentResult(Guid CommentId);

public sealed record FacetValueDto(string Value, int Count);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Data,
    int Page,
    int PageSize,
    int TotalCount,
    string? NextCursor = null)
{
    /// <summary>-1 when total was skipped (<see cref="RepositoryItemListQuery.SkipTotal"/>).</summary>
    public bool TotalSkipped => TotalCount < 0;

    public int TotalPages => TotalSkipped || PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasMore => NextCursor != null;
}
