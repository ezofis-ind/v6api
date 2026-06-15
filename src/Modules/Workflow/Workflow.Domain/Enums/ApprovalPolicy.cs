namespace SaaSApp.Workflow.Domain.Enums;

/// <summary>Approval policy for multi-approver steps.</summary>
public enum ApprovalPolicy
{
    /// <summary>All assigned approvers must approve before moving to next stage.</summary>
    AllMustApprove = 0,

    /// <summary>Any one approval moves to next stage.</summary>
    AnyOneApprove = 1
}
