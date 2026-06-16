namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>
/// Keeps workflow.Inbox_{suffix}, Sent_{suffix}, and Completed_{suffix} (first 8 chars of workflow id, no hyphens)
/// in sync with workflow.transaction_{suffix} rows.
/// </summary>
public interface IWorkflowLegacyMailboxSyncService
{
    /// <summary>Upserts/removes mailbox row for a single transaction row after insert or update.</summary>
    Task SyncTransactionRowAsync(
        Guid workflowId,
        int transactionRowId,
        CancellationToken cancellationToken = default);

    /// <summary>Syncs all END-stage transactions for a workflow instance (e.g. when workflow completes).</summary>
    Task SyncInstanceEndTransactionsAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>Updates formId, formEntryId, and formData on all Inbox/Sent/Completed rows for a workflow instance.</summary>
    Task PropagateInstanceFormDataAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        MailboxFormSnapshot formData,
        CancellationToken cancellationToken = default);
}
