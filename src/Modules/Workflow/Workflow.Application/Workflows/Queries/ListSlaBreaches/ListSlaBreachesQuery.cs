using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Queries.ListSlaBreaches;

/// <summary>List workflow instances with SLA breaches or at risk.</summary>
public record ListSlaBreachesQuery : IRequest<ListSlaBreachesQueryResult>;

/// <summary>List of workflow instances with SLA issues.</summary>
public record ListSlaBreachesQueryResult(IReadOnlyList<SlaBreachItem> Items);

/// <summary>Workflow instance with SLA breach or at-risk status.</summary>
public record SlaBreachItem(
    Guid InstanceId,
    Guid WorkflowId,
    string WorkflowName,
    WorkflowInstanceStatus InstanceStatus,
    SlaPriority Priority,
    SlaStatus ResponseStatus,
    SlaStatus ResolutionStatus,
    DateTime ResponseDeadline,
    DateTime ResolutionDeadline,
    bool IsEscalated,
    DateTime CreatedAtUtc);
