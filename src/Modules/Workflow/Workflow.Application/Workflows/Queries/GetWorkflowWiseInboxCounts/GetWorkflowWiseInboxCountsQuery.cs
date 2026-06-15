using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetWorkflowWiseInboxCounts;

/// <summary>Get inbox count grouped by workflow for current user.</summary>
public record GetWorkflowWiseInboxCountsQuery : IRequest<GetWorkflowWiseInboxCountsQueryResult>;

/// <summary>List of workflows with inbox count for current user.</summary>
public record GetWorkflowWiseInboxCountsQueryResult(List<WorkflowInboxCountItem> Items);

/// <summary>Single workflow with its inbox count.</summary>
public record WorkflowInboxCountItem(Guid WorkflowId, string WorkflowName, int InboxCount);
