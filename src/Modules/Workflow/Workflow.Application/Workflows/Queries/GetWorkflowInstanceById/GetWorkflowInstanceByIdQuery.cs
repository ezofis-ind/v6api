using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetWorkflowInstanceById;

/// <summary>Get a workflow instance by ID with step instances.</summary>
public record GetWorkflowInstanceByIdQuery(Guid InstanceId) : IRequest<GetWorkflowInstanceByIdQueryResult?>;

/// <summary>Workflow instance details with step instances.</summary>
public record GetWorkflowInstanceByIdQueryResult(
    Guid Id,
    Guid WorkflowId,
    string WorkflowName,
    WorkflowInstanceStatus Status,
    Guid? CurrentStepInstanceId,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    Guid StartedBy,
    IReadOnlyList<WorkflowStepInstanceItem> StepInstances);

/// <summary>Step instance summary.</summary>
public record WorkflowStepInstanceItem(Guid Id, string StepName, StepType StepType, int Order, StepInstanceStatus Status, DateTime? StartedAtUtc, DateTime? CompletedAtUtc);
