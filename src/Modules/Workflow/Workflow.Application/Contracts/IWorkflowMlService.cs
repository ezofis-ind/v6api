using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Creates ML prediction models for workflows.</summary>
public interface IWorkflowMlService
{
    Task CreateMlPredictionsAsync(
        Guid workflowId,
        List<WorkflowBlockDto> blocks,
        CancellationToken cancellationToken = default);
}

