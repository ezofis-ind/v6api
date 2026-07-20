using MediatR;
using SaaSApp.Workflow.Application.Workflows;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.UpdateWorkflow;

/// <summary>Update a workflow definition. Only non-null fields are applied. When <see cref="WorkflowJson"/> is set, full v5-style JSON is persisted and side effects mirror create (security, SLA, tables, etc.).</summary>
public record UpdateWorkflowCommand(
    Guid WorkflowId,
    string? Name = null,
    string? Description = null,
    TriggerType? TriggerType = null,
    string? TriggerConfig = null,
    WorkflowJsonDto? WorkflowJson = null,
    bool PublishImmediately = false,
    /// <summary>Original designer JSON as sent by the client (stored in blob verbatim when set).</summary>
    string? WorkflowJsonRaw = null,
    WorkflowEmailIngestOptions? EmailIngest = null) : IRequest<UpdateWorkflowCommandResult>;

/// <summary>Whether the workflow was found and updated. <see cref="NameConflict"/> matches v5 406 when renaming to an existing workflow name.</summary>
public record UpdateWorkflowCommandResult(
    bool Found,
    bool NameConflict = false,
    Guid? EmailIngestMailboxId = null,
    Guid? EmailConnectorId = null,
    bool EmailIngestEnabled = false);
