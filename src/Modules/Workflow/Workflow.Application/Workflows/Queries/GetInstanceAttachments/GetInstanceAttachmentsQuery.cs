using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetInstanceAttachments;

/// <summary>Get attachments for a workflow instance from workflow-specific table.</summary>
public record GetInstanceAttachmentsQuery(Guid InstanceId) : IRequest<GetInstanceAttachmentsQueryResult>;

/// <summary>Attachments from workflow-specific table.</summary>
public record GetInstanceAttachmentsQueryResult(List<AttachmentItem> Attachments, string TableName);

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
    Guid? ModifiedBy,
    DateTime? ModifiedAtUtc,
    Guid? RepositoryId,
    Guid? ItemId
);
