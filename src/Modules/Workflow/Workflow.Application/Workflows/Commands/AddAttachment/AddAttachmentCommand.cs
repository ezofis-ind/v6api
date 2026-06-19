using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.AddAttachment;

/// <summary>Add an attachment to a workflow instance (stored in workflow-specific table).</summary>
public record AddAttachmentCommand(
    Guid WorkflowId,
    Guid WorkflowInstanceId,
    string FileName,
    string FilePath,
    long? FileSize = null,
    string? ContentType = null
) : IRequest<AddAttachmentCommandResult>;

/// <summary>Result of adding an attachment.</summary>
public record AddAttachmentCommandResult(
    Guid AttachmentId,
    Guid WorkflowId,
    Guid WorkflowInstanceId,
    string TableName);
