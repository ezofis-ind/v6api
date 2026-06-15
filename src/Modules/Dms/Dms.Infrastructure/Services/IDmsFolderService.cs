using SaaSApp.Dms.Domain.Models;

namespace SaaSApp.Dms.Infrastructure.Services;

/// <summary>Service for DMS folder tree and document listing.</summary>
public interface IDmsFolderService
{
    /// <summary>Get folder children for file explorer tree. Path: "" (root), "2025", "2025/Purchase", "2025/Purchase/Acme Corp".</summary>
    Task<FolderChildrenResponse> GetFolderChildrenAsync(Guid repositoryId, string tableName, string path, CancellationToken ct = default);

    /// <summary>Get documents in folder. Path must be full: "2025/Purchase/Acme Corp".</summary>
    Task<DocumentListResponse> GetDocumentsInFolderAsync(Guid repositoryId, string tableName, string path, int page, int pageSize, CancellationToken ct = default);
}
