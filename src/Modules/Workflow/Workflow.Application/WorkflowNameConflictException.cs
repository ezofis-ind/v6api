namespace SaaSApp.Workflow.Application;

/// <summary>Thrown when creating a workflow whose name is already used in the tenant.</summary>
public sealed class WorkflowNameConflictException : InvalidOperationException
{
    public WorkflowNameConflictException(string workflowName)
        : base($"Workflow with name '{workflowName}' already exists.")
    {
        WorkflowName = workflowName;
    }

    public string WorkflowName { get; }
}
