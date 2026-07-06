using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class WorkflowInboxShareAssignmentService : IWorkflowInboxShareAssignmentService
{
    private const string EndStageType = "END";
    private const int ActionStatusOpen = 0;

    private readonly ITenantContext _tenantContext;
    private readonly IWorkflowRepository _repository;
    private readonly IWorkflowTableCreator _tableCreator;
    private readonly IWorkflowLegacyMailboxSyncService _mailboxSync;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkflowInboxShareAssignmentService> _logger;

    public WorkflowInboxShareAssignmentService(
        ITenantContext tenantContext,
        IWorkflowRepository repository,
        IWorkflowTableCreator tableCreator,
        IWorkflowLegacyMailboxSyncService mailboxSync,
        IUnitOfWork unitOfWork,
        ILogger<WorkflowInboxShareAssignmentService> logger)
    {
        _tenantContext = tenantContext;
        _repository = repository;
        _tableCreator = tableCreator;
        _mailboxSync = mailboxSync;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<WorkflowInboxShareAssignmentResult> AssignOpenInboxToUserAsync(
        Guid workflowInstanceId,
        Guid guestUserId,
        Guid modifiedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (guestUserId == Guid.Empty)
            throw new ArgumentException("Guest user id is required.", nameof(guestUserId));
        if (modifiedByUserId == Guid.Empty)
            throw new ArgumentException("Modified by user id is required.", nameof(modifiedByUserId));

        var instance = await _repository.GetInstanceByIdAsync(workflowInstanceId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow instance not found.");

        if (instance.Status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Cancelled)
            throw new InvalidOperationException("Cannot assign inbox for a completed or cancelled workflow.");

        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        var workflowId = instance.WorkflowId;
        await _tableCreator.EnsureLegacyTransactionTableAsync(workflowId, connectionString, cancellationToken);
        await _tableCreator.EnsureLegacyMailboxTablesAsync(workflowId, connectionString, cancellationToken);

        var suffix = workflowId.ToString("N")[..8];
        var transactionTable = $"workflow.[transaction_{suffix}]";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var openTransaction = await FindOpenTransactionAsync(
            connection, transactionTable, workflowInstanceId, cancellationToken);

        if (openTransaction == null)
        {
            _logger.LogWarning(
                "No open inbox transaction for workflow instance {InstanceId}; guest {GuestUserId} not assigned.",
                workflowInstanceId,
                guestUserId);
            return new WorkflowInboxShareAssignmentResult(
                workflowId, workflowInstanceId, 0, null, guestUserId, InboxAssigned: false);
        }

        await UpdateTransactionAssigneeAsync(
            connection,
            transactionTable,
            openTransaction.Value.TransactionId,
            guestUserId,
            modifiedByUserId,
            cancellationToken);

        instance.Reassign(guestUserId);
        await _repository.UpdateInstanceAsync(instance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _mailboxSync.SyncTransactionRowAsync(
            workflowId,
            openTransaction.Value.TransactionId,
            cancellationToken);

        return new WorkflowInboxShareAssignmentResult(
            workflowId,
            workflowInstanceId,
            openTransaction.Value.TransactionId,
            openTransaction.Value.ActivityId,
            guestUserId,
            InboxAssigned: true);
    }

    private static async Task<(int TransactionId, string? ActivityId)?> FindOpenTransactionAsync(
        SqlConnection connection,
        string transactionTable,
        Guid workflowInstanceId,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT TOP 1 Id, ActivityId
FROM {transactionTable}
WHERE WorkflowInstanceId = @WorkflowInstanceId
  AND IsDeleted = 0
  AND ActionStatus = @ActionStatusOpen
  AND UPPER(LTRIM(RTRIM(ISNULL(StageType, N'')))) <> @EndStageType
ORDER BY Id DESC;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);
        cmd.Parameters.AddWithValue("@ActionStatusOpen", ActionStatusOpen);
        cmd.Parameters.AddWithValue("@EndStageType", EndStageType);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static async Task UpdateTransactionAssigneeAsync(
        SqlConnection connection,
        string transactionTable,
        int transactionId,
        Guid guestUserId,
        Guid modifiedByUserId,
        CancellationToken cancellationToken)
    {
        var sql = $@"
UPDATE {transactionTable}
SET ActivityUserId = @ActivityUserId,
    ModifiedAt = SYSUTCDATETIME(),
    ModifiedBy = @ModifiedBy
WHERE Id = @Id AND IsDeleted = 0;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ActivityUserId", guestUserId);
        cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
        cmd.Parameters.AddWithValue("@Id", transactionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
