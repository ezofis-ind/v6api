namespace SaaSApp.Dms.Domain.Models;

/// <summary>Folder node for file explorer tree.</summary>
public record FolderNode(string Name, string Path, int DocumentCount);

/// <summary>Response for folder children (tree nodes).</summary>
public record FolderChildrenResponse(string Path, IReadOnlyList<FolderNode> Children);

/// <summary>Document list item for grid view.</summary>
public record DocumentListItem(
    Guid Id,
    string FileName,
    int Status,
    int SignStatus,
    DateTime CreatedAt,
    Guid? WorkflowInstanceId,
    string? ReportNo,
    string? ReferenceNo);

/// <summary>Response for documents in folder.</summary>
public record DocumentListResponse(IReadOnlyList<DocumentListItem> Items, int TotalCount);
