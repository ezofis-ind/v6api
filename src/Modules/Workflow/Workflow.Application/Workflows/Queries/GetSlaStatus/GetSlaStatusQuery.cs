using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetSlaStatus;

/// <summary>Get SLA status for a workflow instance.</summary>
public record GetSlaStatusQuery(Guid InstanceId) : IRequest<GetSlaStatusQueryResult?>;

/// <summary>SLA status for a workflow instance.</summary>
public record GetSlaStatusQueryResult(
    Guid InstanceId,
    SlaPriority Priority,
    DateTime ResponseDeadline,
    DateTime ResolutionDeadline,
    DateTime? EscalationDeadline,
    DateTime? ResponseAchievedAt,
    DateTime? ResolutionAchievedAt,
    SlaStatus ResponseStatus,
    SlaStatus ResolutionStatus,
    bool IsEscalated,
    TimeSpan ResponseTimeRemaining,
    TimeSpan ResolutionTimeRemaining);
