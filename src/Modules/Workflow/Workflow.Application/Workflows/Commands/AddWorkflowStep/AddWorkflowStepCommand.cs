using MediatR;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.AddWorkflowStep;

/// <summary>Add a step to a workflow definition. Workflow must be in Draft status.</summary>
public record AddWorkflowStepCommand(
    Guid WorkflowId,
    string Name,
    StepType StepType,
    int Order,
    string? Description = null,
    string? Config = null,
    bool IsRequired = true,
    Guid? AssignedToUserId = null,
    string? AssignedToRole = null,
    Guid? ApprovedNextStepId = null,
    Guid? RejectedNextStepId = null,
    ApprovalPolicy ApprovalPolicy = ApprovalPolicy.AnyOneApprove,
    string? ApproversJson = null,
    string? ActivityId = null
) : IRequest<AddWorkflowStepCommandResult>;

/// <summary>Result of AddWorkflowStep.</summary>
public record AddWorkflowStepCommandResult(Guid StepId, bool Found);
