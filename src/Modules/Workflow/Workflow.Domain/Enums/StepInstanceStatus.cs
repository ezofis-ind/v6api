namespace SaaSApp.Workflow.Domain.Enums;

/// <summary>Workflow step instance execution status.</summary>
public enum StepInstanceStatus
{
    /// <summary>Not yet started.</summary>
    Pending = 0,
    
    /// <summary>Currently executing.</summary>
    InProgress = 1,
    
    /// <summary>Completed successfully.</summary>
    Completed = 2,
    
    /// <summary>Failed due to error.</summary>
    Failed = 3,
    
    /// <summary>Skipped (conditional branch not taken).</summary>
    Skipped = 4,
    
    /// <summary>Waiting for approval.</summary>
    WaitingForApproval = 5
}
