namespace SaaSApp.Workflow.Domain.Enums;

/// <summary>Approval request status.</summary>
public enum ApprovalStatus
{
    /// <summary>Pending approval.</summary>
    Pending = 0,
    
    /// <summary>Approved.</summary>
    Approved = 1,
    
    /// <summary>Rejected.</summary>
    Rejected = 2,
    
    /// <summary>Cancelled (workflow cancelled or step skipped).</summary>
    Cancelled = 3
}
