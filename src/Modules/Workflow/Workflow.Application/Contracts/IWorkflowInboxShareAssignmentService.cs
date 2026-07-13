namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>
/// Reassigns the open workflow inbox task to a guest user invited via file share.
/// </summary>
public interface IWorkflowInboxShareAssignmentService
{
  /// <summary>
  /// Points the open legacy transaction (and mailbox inbox row) at the guest user
  /// so they can verify/approve after first login.
  /// </summary>
  /// <param name="action">
  /// Inbox <c>action</c> flag: 1 = show verify/approve (default), 0 = hide action buttons.
  /// </param>
  Task<WorkflowInboxShareAssignmentResult> AssignOpenInboxToUserAsync(
      Guid workflowInstanceId,
      Guid guestUserId,
      Guid modifiedByUserId,
      int action = 1,
      CancellationToken cancellationToken = default);
}

public sealed record WorkflowInboxShareAssignmentResult(
    Guid WorkflowId,
    Guid WorkflowInstanceId,
    int TransactionId,
    string? ActivityId,
    Guid GuestUserId,
    bool InboxAssigned);
