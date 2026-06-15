namespace SaaSApp.Repository.Application.Contracts;

/// <summary>
/// Upload with archive folder layout: archive/{repositoryName}/{level1}/.../{leaf}.ext
/// and <c>repository.Folders</c> rows (ParentId chain) from RepositoryFields levels.
/// </summary>
public interface IRepositoryArchiveFileUploadService
{
    Task<RepositoryArchiveUploadItemResult> UploadItemAsync(
        Guid repositoryId,
        Guid tenantId,
        RepositoryUploadItemRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default);
}

public sealed record RepositoryArchiveUploadItemResult(
    Guid ItemId,
    string FileName,
    string FilePath,
    string StorageProviderCode,
    int FileVersion,
    Guid FolderId,
    IReadOnlyList<string> FolderPathSegments,
    string RepositoryName,
    bool WorkflowAttached,
    Guid? WorkflowId,
    int? ProcessId,
    Guid? InstanceId);
