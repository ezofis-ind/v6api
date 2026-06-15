using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.SetWorkflowSla;

/// <summary>Set or update SLA policy for a workflow.</summary>
public record SetWorkflowSlaCommand(Guid WorkflowId, SlaPriority Priority, int ResponseTimeMinutes, int ResolutionTimeMinutes, int? EscalationTimeMinutes = null, Guid? EscalateToUserId = null, string? EscalateToRole = null, bool SendNotificationOnBreach = true, string? NotificationEmails = null) : IRequest<SetWorkflowSlaCommandResult>;

/// <summary>Result of SetWorkflowSla.</summary>
public record SetWorkflowSlaCommandResult(Guid SlaId);
