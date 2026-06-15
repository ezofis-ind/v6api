using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.AddComment;

/// <summary>Add a comment to a workflow instance (stored in workflow-specific table).</summary>
public record AddCommentCommand(
    Guid WorkflowInstanceId,
    string Comments,
    Guid? StepInstanceId = null,
    string? ExternalCommentsBy = null,
    int ShowTo = 0
) : IRequest<AddCommentCommandResult>;

/// <summary>Result of adding a comment.</summary>
public record AddCommentCommandResult(Guid CommentId, string TableName);
