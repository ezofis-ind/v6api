using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetMyCompleted;

/// <summary>Get completed workflow instances for current user.</summary>
public record GetMyCompletedQuery(int PageNumber = 1, int PageSize = 20) : IRequest<GetMyCompletedQueryResult>;

/// <summary>Paginated list of completed workflow instances.</summary>
public record GetMyCompletedQueryResult(
    List<CompletedWorkflowItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

/// <summary>Completed workflow instance.</summary>
public record CompletedWorkflowItem(
    Guid InstanceId,
    Guid WorkflowId,
    string WorkflowName,
    string? ReferenceNumber,
    string? CustomerName,
    int Priority,
    DateTime CreatedAtUtc,
    DateTime CompletedAtUtc,
    TimeSpan Duration,
    bool WasCreatedByMe,
    bool WasAssignedToMe
);
