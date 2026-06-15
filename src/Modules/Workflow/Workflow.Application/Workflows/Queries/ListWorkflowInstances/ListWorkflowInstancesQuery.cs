using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Queries.ListWorkflowInstances;

/// <summary>List workflow instances for a workflow.</summary>
public record ListWorkflowInstancesQuery(Guid WorkflowId) : IRequest<ListWorkflowInstancesQueryResult>;

/// <summary>List of workflow instances.</summary>
public record ListWorkflowInstancesQueryResult(IReadOnlyList<WorkflowInstanceItem> Items);

/// <summary>Workflow instance summary.</summary>
public record WorkflowInstanceItem(Guid Id, Guid WorkflowId, string WorkflowName, WorkflowInstanceStatus Status, DateTime CreatedAtUtc, DateTime? StartedAtUtc, DateTime? CompletedAtUtc, Guid StartedBy);
