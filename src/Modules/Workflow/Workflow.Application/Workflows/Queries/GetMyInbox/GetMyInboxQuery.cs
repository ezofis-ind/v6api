using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetMyInbox;

/// <summary>Get workflow instances assigned to current user (My Inbox). Optional workflowId to filter by workflow.</summary>
public record GetMyInboxQuery(int PageNumber = 1, int PageSize = 20, Guid? WorkflowId = null) : IRequest<GetMyInboxQueryResult>;

/// <summary>Paginated list of workflow instances in user's inbox.</summary>
public record GetMyInboxQueryResult(
    List<InboxWorkflowItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

/// <summary>Workflow instance in inbox.</summary>
public record InboxWorkflowItem(
    Guid InstanceId,
    Guid WorkflowId,
    string WorkflowName,
    string? ReferenceNumber,
    string? CustomerName,
    WorkflowInstanceStatus Status,
    int Priority,
    DateTime CreatedAtUtc,
    DateTime? LastActivityAtUtc,
    bool HasSla,
    string? SlaStatus
);
