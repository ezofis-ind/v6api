using SaaSApp.Workflow.Domain.Entities;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows;

/// <summary>Shared step-instance transitions used by move-next and start bootstrap.</summary>
public static class WorkflowStepTransitionHelper
{
    public const string StartProceedReview = "Submit";

    public static WorkflowStep? ResolveApAgentStep(IReadOnlyList<WorkflowStep> orderedSteps)
    {
        var byType = orderedSteps.FirstOrDefault(IsApAgentStep);
        if (byType != null)
            return byType;

        var byName = orderedSteps.FirstOrDefault(s =>
            string.Equals(s.Name, "Ap Agent", StringComparison.OrdinalIgnoreCase));
        if (byName != null)
            return byName;

        return orderedSteps.FirstOrDefault(s => s.Order == 2);
    }

    public static bool IsApAgentStep(WorkflowStep step) =>
        string.Equals(step.StageType, "AP_AGENT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(step.Name, "Ap Agent", StringComparison.OrdinalIgnoreCase);

    public static bool IsApproveReview(string? review) =>
        string.Equals(review?.Trim(), "Approve", StringComparison.OrdinalIgnoreCase);

    public static WorkflowStepInstance? FindStepInstance(WorkflowInstance instance, Guid workflowStepId) =>
        instance.StepInstances.FirstOrDefault(s => s.WorkflowStepId == workflowStepId);

    public static void CompleteStepInstance(WorkflowInstance instance, Guid workflowStepId, Guid userId)
    {
        var si = FindStepInstance(instance, workflowStepId);
        if (si != null && si.Status is StepInstanceStatus.InProgress or StepInstanceStatus.WaitingForApproval)
            si.Complete(userId);
    }

    public static void StartStepInstance(WorkflowInstance instance, Guid workflowStepId)
    {
        var si = FindStepInstance(instance, workflowStepId);
        if (si != null && si.Status == StepInstanceStatus.Pending)
        {
            si.Start();
            instance.SetCurrentStep(si.Id);
        }
    }
}
