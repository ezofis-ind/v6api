using System.Text.Json;
using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetWorkflowById;

/// <summary>Get a workflow by ID in the current tenant.</summary>
public record GetWorkflowByIdQuery(Guid WorkflowId) : IRequest<GetWorkflowByIdQueryResult?>;

/// <summary>Workflow details for GetById response (includes designer flow JSON from blob/file storage).</summary>
public record GetWorkflowByIdQueryResult(
    Guid Id,
    string Name,
    string? Description,
    WorkflowStatus Status,
    TriggerType TriggerType,
    string? TriggerConfig,
    int Version,
    DateTime CreatedAtUtc,
    IReadOnlyList<WorkflowStepItem> Steps,
    JsonElement? WorkflowJson = null,
    string? RepositoryId = null,
    string? FormId = null);

/// <summary>Workflow step summary.</summary>
public record WorkflowStepItem(Guid Id, string Name, string? Description, StepType StepType, int Order, bool IsRequired, Guid? AssignedToUserId, string? AssignedToRole, string? ActivityId = null, string? StageType = null);
