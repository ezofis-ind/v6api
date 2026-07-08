using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetInstanceAttachments;

/// <summary>Get attachments for a workflow instance from workflow-specific table.</summary>
public record GetInstanceAttachmentsQuery(Guid WorkflowId, Guid InstanceId) : IRequest<GetInstanceAttachmentsQueryResult>;

/// <summary>Attachments from workflow-specific table.</summary>
public record GetInstanceAttachmentsQueryResult(
    Guid WorkflowId,
    Guid InstanceId,
    List<AttachmentItem> Attachments,
    string TableName);

/// <summary>Attachment item.</summary>
public record AttachmentItem(
    Guid Id,
    Guid WorkflowInstanceId,
    Guid WorkflowId,
    string FileName,
    string FilePath,
    long? FileSize,
    string? ContentType,
    DateTime CreatedAtUtc,
    Guid CreatedBy,
    string? UploadedBy,
    Guid? ModifiedBy,
    DateTime? ModifiedAtUtc,
    Guid? RepositoryId,
    Guid? ItemId
);
