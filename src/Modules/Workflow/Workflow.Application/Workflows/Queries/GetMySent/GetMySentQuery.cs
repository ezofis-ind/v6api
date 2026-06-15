using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetMySent;

/// <summary>Get workflow instances created by current user (My Sent).</summary>
public record GetMySentQuery(int PageNumber = 1, int PageSize = 20) : IRequest<GetMySentQueryResult>;

/// <summary>Paginated list of workflow instances created by user.</summary>
public record GetMySentQueryResult(
    List<SentWorkflowItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

/// <summary>Workflow instance created by user.</summary>
public record SentWorkflowItem(
    Guid InstanceId,
    Guid WorkflowId,
    string WorkflowName,
    string? ReferenceNumber,
    string? CustomerName,
    WorkflowInstanceStatus Status,
    int Priority,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    Guid? AssignedToUserId
);
