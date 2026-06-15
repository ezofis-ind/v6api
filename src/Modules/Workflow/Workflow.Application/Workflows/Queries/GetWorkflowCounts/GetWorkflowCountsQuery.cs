using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetWorkflowCounts;

/// <summary>Get workflow instance counts (Inbox, Sent, Completed) for current user.</summary>
public record GetWorkflowCountsQuery : IRequest<GetWorkflowCountsQueryResult>;

/// <summary>Workflow counts for dashboard/overview.</summary>
public record GetWorkflowCountsQueryResult(
    int InboxCount,      // Assigned to me, pending/running
    int SentCount,       // Created by me
    int CompletedCount,  // Completed workflows
    int TotalActive      // All active (not archived)
);
