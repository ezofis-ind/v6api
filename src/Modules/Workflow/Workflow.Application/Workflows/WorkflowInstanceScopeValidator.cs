using SaaSApp.Workflow.Domain.Entities;

namespace SaaSApp.Workflow.Application.Workflows;

internal static class WorkflowInstanceScopeValidator
{
    public static void EnsureInstanceBelongsToWorkflow(WorkflowInstance? instance, Guid workflowId, Guid instanceId)
    {
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        if (instance.Id != instanceId)
            throw new InvalidOperationException("Workflow instance not found.");

        if (instance.WorkflowId != workflowId)
            throw new InvalidOperationException("Workflow instance does not belong to the specified workflow.");
    }
}
