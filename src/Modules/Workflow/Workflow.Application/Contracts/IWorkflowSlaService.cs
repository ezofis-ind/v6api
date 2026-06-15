using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Creates SLA rules for workflows.</summary>
public interface IWorkflowSlaService
{
    Task CreateSlaRulesAsync(
        Guid workflowId,
        List<WorkflowSlaRuleDto>? generalSlaRules,
        List<WorkflowBlockDto> blocks,
        CancellationToken cancellationToken = default);
}

