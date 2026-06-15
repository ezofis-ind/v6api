using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetInstanceComments;

/// <summary>Get comments for a workflow instance from workflow-specific table.</summary>
public record GetInstanceCommentsQuery(Guid InstanceId) : IRequest<GetInstanceCommentsQueryResult>;

/// <summary>Comments from workflow-specific table.</summary>
public record GetInstanceCommentsQueryResult(List<CommentItem> Comments, string TableName);

/// <summary>Comment item.</summary>
public record CommentItem(
    Guid Id,
    Guid WorkflowInstanceId,
    Guid? StepInstanceId,
    string Comments,
    string? ExternalCommentsBy,
    int ShowTo,
    string? EmbedJson,
    bool EmbedStatus,
    DateTime CreatedAtUtc,
    Guid CreatedBy
);
