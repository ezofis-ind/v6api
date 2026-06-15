namespace SaaSApp.Workflow.Domain.Enums;

/// <summary>Type of workflow step.</summary>
public enum StepType
{
    /// <summary>Manual task assigned to a user.</summary>
    Manual = 0,
    
    /// <summary>Approval required from one or more users.</summary>
    Approval = 1,
    
    /// <summary>Automated action (API call, script, etc.).</summary>
    Automated = 2,
    
    /// <summary>Conditional branch (if/else logic).</summary>
    Condition = 3,
    
    /// <summary>Wait for external event or webhook.</summary>
    WaitForEvent = 4,
    
    /// <summary>Send notification (email, SMS, etc.).</summary>
    Notification = 5
}
