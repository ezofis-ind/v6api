using MediatR;
using SaaSApp.Workflow.Application.Workflows;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

/// <summary>Create a workflow definition in the current tenant.</summary>
public record CreateWorkflowCommand(
    string Name, 
    string? Description, 
    TriggerType TriggerType, 
    string? TriggerConfig = null,
    WorkflowJsonDto? WorkflowJson = null,  // Full workflow JSON from source API
    bool PublishImmediately = false,
    /// <summary>Original designer JSON as sent by the client (stored in blob verbatim when set).</summary>
    string? WorkflowJsonRaw = null,
    WorkflowEmailIngestOptions? EmailIngest = null
) : IRequest<CreateWorkflowCommandResult>;

/// <summary>Result of CreateWorkflow. Contains the new workflow ID and status.</summary>
public record CreateWorkflowCommandResult(
    Guid WorkflowId,
    bool IsPublished,
    int? RepositoryId = null,
    Guid? RepositoryGuid = null,
    int? FormId = null,
    Guid? FormGuid = null,
    Guid? EmailIngestMailboxId = null,
    Guid? EmailConnectorId = null,
    bool EmailIngestEnabled = false
);
