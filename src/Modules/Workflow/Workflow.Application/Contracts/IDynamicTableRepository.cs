namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>
/// Repository for managing workflow-specific dynamic tables.
/// Each workflow has its own set of tables (Comments_X, Attachments_X, etc.)
/// </summary>
public interface IDynamicTableRepository
{
    /// <summary>Get table name for a specific workflow and entity type.</summary>
    string GetTableName(Guid workflowId, string entityType);
    
    /// <summary>Add comment to workflow-specific table.</summary>
    Task AddCommentAsync(Guid workflowId, Guid workflowInstanceId, string comments, Guid createdBy, Guid? stepInstanceId = null, string? externalCommentsBy = null, int showTo = 0, CancellationToken cancellationToken = default);
    
    /// <summary>Add attachment to workflow-specific table.</summary>
    Task<Guid> AddAttachmentAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        string fileName,
        string filePath,
        Guid createdBy,
        long? fileSize = null,
        string? contentType = null,
        Guid? stepInstanceId = null,
        Guid? repositoryId = null,
        Guid? itemId = null,
        string? formJsonId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get comments for a workflow instance.</summary>
    Task<List<dynamic>> GetCommentsAsync(Guid workflowId, Guid workflowInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>Get attachments for a workflow instance.</summary>
    Task<IReadOnlyList<WorkflowAttachmentRowDto>> GetAttachmentsAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);
}

/// <summary>Row from workflow.WorkflowAttachments_{suffix}.</summary>
public sealed record WorkflowAttachmentRowDto(
    Guid Id,
    Guid WorkflowInstanceId,
    string? FileName,
    string? FilePath,
    long? FileSize,
    string? ContentType,
    DateTime CreatedAtUtc,
    Guid CreatedBy,
    DateTime? ModifiedAtUtc,
    Guid? ModifiedBy,
    Guid? RepositoryId,
    Guid? ItemId,
    string? FormJsonId);
