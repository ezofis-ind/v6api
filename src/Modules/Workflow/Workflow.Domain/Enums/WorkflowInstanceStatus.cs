namespace SaaSApp.Workflow.Domain.Enums;

/// <summary>Workflow instance execution status.</summary>
public enum WorkflowInstanceStatus
{
    /// <summary>Not yet started.</summary>
    Pending = 0,
    
    /// <summary>Currently executing.</summary>
    Running = 1,
    
    /// <summary>Paused by user or system.</summary>
    Paused = 2,
    
    /// <summary>Completed successfully.</summary>
    Completed = 3,
    
    /// <summary>Failed due to error.</summary>
    Failed = 4,
    
    /// <summary>Cancelled by user.</summary>
    Cancelled = 5
}
