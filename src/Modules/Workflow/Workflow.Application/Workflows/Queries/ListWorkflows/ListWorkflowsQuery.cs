using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Queries.ListWorkflows;

/// <summary>List all workflows in the current tenant (excluding soft-deleted).</summary>
public record ListWorkflowsQuery : IRequest<ListWorkflowsQueryResult>;

/// <summary>List of workflows for ListWorkflows response.</summary>
public record ListWorkflowsQueryResult(IReadOnlyList<ListWorkflowsItem> Items);

/// <summary>Workflow summary in list response.</summary>
public record ListWorkflowsItem(
    Guid Id,
    string Name,
    string? Description,
    WorkflowStatus Status,
    TriggerType TriggerType,
    int Version,
    DateTime CreatedAtUtc,
    Guid CreatedBy,
    Guid? ModifiedBy = null,
    DateTime? ModifiedAtUtc = null,
    /// <summary>Creator email from users.Users.</summary>
    string? CreatedByName = null,
    /// <summary>Modifier email if modifiedBy is set; otherwise creator email.</summary>
    string? ModifiedByName = null);
