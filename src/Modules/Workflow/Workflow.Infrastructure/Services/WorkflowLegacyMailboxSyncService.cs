using System.Globalization;
using Microsoft.Data.SqlClient;

using Microsoft.Extensions.Logging;

using SaaSApp.Workflow.Application.Contracts;



namespace SaaSApp.Workflow.Infrastructure.Services;



/// <summary>

/// Routes transaction rows to workflow.Inbox_*, Sent_*, or Completed_*.

/// Deduplicates by workflowId + workflowInstanceId + activityId (delete all matches, then insert one row).

/// </summary>

public sealed class WorkflowLegacyMailboxSyncService : IWorkflowLegacyMailboxSyncService

{

    private const string EndStageType = "END";



    private readonly ITenantContext _tenantContext;

    private readonly IWorkflowTableCreator _tableCreator;

    private readonly ILogger<WorkflowLegacyMailboxSyncService> _logger;

    private sealed record MailboxExtraData(
        Guid? RepositoryId,
        Guid? ItemId,
        string? FormId,
        string? FormEntryId,
        string? FormData);



    public WorkflowLegacyMailboxSyncService(

        ITenantContext tenantContext,

        IWorkflowTableCreator tableCreator,

        ILogger<WorkflowLegacyMailboxSyncService> logger)

    {

        _tenantContext = tenantContext;

        _tableCreator = tableCreator;

        _logger = logger;

    }



    public async Task SyncTransactionRowAsync(
        Guid workflowId,
        int transactionRowId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await SyncTransactionRowAsync(workflowId, transactionRowId, connection, formOverride: null, cancellationToken);
    }

    public async Task SyncInstanceEndTransactionsAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await SyncInstanceEndTransactionsAsync(workflowId, workflowInstanceId, connection, formOverride: null, cancellationToken);
    }

    /// <summary>Sync using an existing open connection (same request/transaction).</summary>
    public async Task SyncTransactionRowAsync(

        Guid workflowId,

        int transactionRowId,

        SqlConnection connection,

        MailboxFormSnapshot? formOverride = null,

        CancellationToken cancellationToken = default)

    {

        var connectionString = _tenantContext.ConnectionString;

        if (!string.IsNullOrWhiteSpace(connectionString))

            await _tableCreator.EnsureLegacyMailboxTablesAsync(workflowId, connectionString, cancellationToken);



        await SyncTransactionRowCoreAsync(workflowId, transactionRowId, connection, formOverride, cancellationToken);

    }



    public async Task SyncInstanceEndTransactionsAsync(

        Guid workflowId,

        Guid workflowInstanceId,

        SqlConnection connection,

        MailboxFormSnapshot? formOverride = null,

        CancellationToken cancellationToken = default)

    {

        var connectionString = _tenantContext.ConnectionString;

        if (!string.IsNullOrWhiteSpace(connectionString))

            await _tableCreator.EnsureLegacyMailboxTablesAsync(workflowId, connectionString, cancellationToken);



        var suffix = workflowId.ToString("N")[..8];

        var transactionTable = $"workflow.[transaction_{suffix}]";

        await SyncInstanceEndTransactionsCoreAsync(workflowId, workflowInstanceId, transactionTable, connection, formOverride, cancellationToken);

    }



    private async Task SyncInstanceEndTransactionsCoreAsync(

        Guid workflowId,

        Guid workflowInstanceId,

        string transactionTable,

        SqlConnection connection,

        MailboxFormSnapshot? formOverride,

        CancellationToken cancellationToken)

    {

        var ids = new List<int>();

        var sql = $@"

SELECT Id

FROM {transactionTable}

WHERE WorkflowInstanceId = @WorkflowInstanceId AND IsDeleted = 0 AND UPPER(LTRIM(RTRIM(StageType))) = @EndStageType;";

        await using (var cmd = new SqlCommand(sql, connection))

        {

            cmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);

            cmd.Parameters.AddWithValue("@EndStageType", EndStageType);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))

                ids.Add(reader.GetInt32(0));

        }



        foreach (var id in ids)

            await SyncTransactionRowCoreAsync(workflowId, id, connection, formOverride, cancellationToken);

    }



    private async Task SyncTransactionRowCoreAsync(

        Guid workflowId,

        int transactionRowId,

        SqlConnection connection,

        MailboxFormSnapshot? formOverride,

        CancellationToken cancellationToken)

    {

        var suffix = workflowId.ToString("N")[..8];
        var workflowIdCompact = workflowId.ToString("N");
        var workflowIdValue = workflowId.ToString("D");

        var transactionTable = $"workflow.[transaction_{suffix}]";

        var instancesTable = $"workflow.[WorkflowInstances_{suffix}]";



        var stateSql = $@"

SELECT

    t.WorkflowInstanceId,

    t.ActivityId,

    t.TransactionGuid,

    t.StageType,

    t.ActionStatus,

    t.IsDeleted

FROM {transactionTable} t

WHERE t.Id = @TransactionRowId;";



        Guid workflowInstanceId;

        string? activityId;

        Guid? transactionGuid;

        string? stageType;

        int actionStatus;

        bool isDeleted;



        await using (var stateCmd = new SqlCommand(stateSql, connection))

        {

            stateCmd.Parameters.AddWithValue("@TransactionRowId", transactionRowId);

            await using var reader = await stateCmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))

                return;



            workflowInstanceId = reader.GetGuid(0);

            activityId = reader.IsDBNull(1) ? null : reader.GetString(1);

            transactionGuid = reader.IsDBNull(2) ? null : reader.GetGuid(2);

            stageType = reader.IsDBNull(3) ? null : reader.GetString(3);

            actionStatus = reader.GetInt32(4);

            isDeleted = reader.GetBoolean(5);

        }



        var workflowInstanceIdStr = workflowInstanceId.ToString("D");

        var inboxTable = MailboxTable("Inbox", suffix);

        var sentTable = MailboxTable("Sent", suffix);

        var completedTable = MailboxTable("Completed", suffix);



        await DeleteFromAllMailboxTablesByKeyAsync(
            connection,
            workflowIdValue,
            workflowIdCompact,
            workflowInstanceId,
            workflowInstanceIdStr,
            activityId,
            inboxTable,
            sentTable,
            completedTable,
            cancellationToken);

        if (isDeleted)
            return;

        var isEnd = string.Equals(stageType?.Trim(), EndStageType, StringComparison.OrdinalIgnoreCase);

        var targetTable = isEnd
            ? completedTable
            : actionStatus == 0
                ? inboxTable
                : sentTable;

        // Keep mailbox aligned with workflow state: no stale inbox after approve; no inbox/sent after complete.
        if (targetTable == sentTable)
            await DeleteMailboxRowsForInstanceAsync(connection, workflowIdValue, workflowIdCompact, workflowInstanceId, workflowInstanceIdStr, inboxTable, cancellationToken);
        else if (targetTable == completedTable)
        {
            await DeleteMailboxRowsForInstanceAsync(connection, workflowIdValue, workflowIdCompact, workflowInstanceId, workflowInstanceIdStr, inboxTable, cancellationToken);
            await DeleteMailboxRowsForInstanceAsync(connection, workflowIdValue, workflowIdCompact, workflowInstanceId, workflowInstanceIdStr, sentTable, cancellationToken);
        }
        else
            await DeleteMailboxRowsForInstanceAsync(connection, workflowIdValue, workflowIdCompact, workflowInstanceId, workflowInstanceIdStr, inboxTable, cancellationToken);



        var txIdStr = transactionGuid is { } g && g != Guid.Empty

            ? g.ToString("D")

            : transactionRowId.ToString();

        var extras = await ResolveMailboxExtraDataAsync(
            connection,
            suffix,
            workflowId,
            workflowInstanceId,
            formOverride,
            cancellationToken);



        var sourceSql = BuildMailboxSourceSelect(transactionTable, instancesTable);

        var insertSql = $@"

INSERT INTO {targetTable}

    (userId, groupId, workflowId, name, workflowInstanceId, referenceNumber, createdAtUtc, startedAtUtc, completedAtUtc, context,

     transactionId, activityId, ruleId, stageType, stage, review,

     transaction_createdAt, transaction_createdBy, transaction_createdByEmail,

     transaction_modifiedAt, transaction_modifiedBy, activityUserEmail,
     repositoryId, itemId, formId, formEntryId, formData)

{sourceSql};";



        await using var insertCmd = new SqlCommand(insertSql, connection);

        insertCmd.Parameters.AddWithValue("@WorkflowGuid", workflowId);

        insertCmd.Parameters.AddWithValue("@WorkflowIdValue", workflowIdValue);

        insertCmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);

        insertCmd.Parameters.AddWithValue("@WorkflowInstanceIdStr", workflowInstanceIdStr);

        insertCmd.Parameters.AddWithValue("@TransactionRowId", transactionRowId);

        insertCmd.Parameters.AddWithValue("@TxGuidStr", txIdStr);
        insertCmd.Parameters.AddWithValue("@RepositoryId", (object?)extras.RepositoryId?.ToString("D") ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@ItemId", (object?)extras.ItemId?.ToString("D") ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@FormId", (object?)extras.FormId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@FormEntryId", (object?)extras.FormEntryId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@FormData", (object?)extras.FormData ?? DBNull.Value);

        await insertCmd.ExecuteNonQueryAsync(cancellationToken);

    }



    private static string BuildMailboxSourceSelect(string transactionTable, string instancesTable)

    {

        return $@"

    SELECT

        CONVERT(NVARCHAR(100), t.ActivityUserId) AS userId,

        t.ActivityGroupId AS groupId,

        @WorkflowIdValue AS workflowId,

        w.Name AS name,

        @WorkflowInstanceIdStr AS workflowInstanceId,

        wi.ReferenceNumber AS referenceNumber,

        wi.CreatedAtUtc AS createdAtUtc,

        wi.StartedAtUtc AS startedAtUtc,

        wi.CompletedAtUtc AS completedAtUtc,

        wi.Context AS context,

        @TxGuidStr AS transactionId,

        t.ActivityId AS activityId,

        t.RuleId AS ruleId,

        t.StageType AS stageType,

        t.StageName AS stage,

        t.Review AS review,

        CAST(t.CreatedAt AS datetime) AS transaction_createdAt,

        CONVERT(NVARCHAR(255), t.CreatedBy) AS transaction_createdBy,

        cu.Email AS transaction_createdByEmail,

        CAST(t.ModifiedAt AS datetime) AS transaction_modifiedAt,

        CONVERT(NVARCHAR(255), t.ModifiedBy) AS transaction_modifiedBy,

        au.Email AS activityUserEmail,

        @RepositoryId AS repositoryId,

        @ItemId AS itemId,

        @FormId AS formId,

        @FormEntryId AS formEntryId,

        @FormData AS formData

    FROM {transactionTable} t

    INNER JOIN {instancesTable} wi ON wi.Id = t.WorkflowInstanceId

    LEFT JOIN workflow.Workflows w ON w.Id = @WorkflowGuid AND w.IsDeleted = 0

    LEFT JOIN users.Users cu ON cu.Id = t.CreatedBy AND cu.IsDeleted = 0

    LEFT JOIN users.Users au ON au.Id = t.ActivityUserId AND au.IsDeleted = 0

    WHERE t.Id = @TransactionRowId AND t.IsDeleted = 0";

    }



    /// <summary>

    /// Removes any existing mailbox row for the same workflow + instance + activity (all three tables).

    /// </summary>

    private static async Task DeleteFromAllMailboxTablesByKeyAsync(
        SqlConnection connection,
        string workflowIdValue,
        string workflowTableKey,
        Guid workflowInstanceId,
        string workflowInstanceIdStr,
        string? activityId,

        string inboxTable,

        string sentTable,

        string completedTable,

        CancellationToken cancellationToken)

    {

        const string keyPredicate = """

            (workflowId = @WorkflowIdValue OR workflowId = @WorkflowTableKey)

            AND (

                workflowInstanceId = @WorkflowInstanceIdStr

                OR TRY_CONVERT(UNIQUEIDENTIFIER, workflowInstanceId) = @WorkflowInstanceId

            )

            AND (

                (@ActivityId IS NULL AND (activityId IS NULL OR LTRIM(RTRIM(activityId)) = N''))

                OR LTRIM(RTRIM(activityId)) = LTRIM(RTRIM(@ActivityId))

            )

            """;



        var sql = $@"

DELETE FROM {inboxTable} WHERE {keyPredicate};

DELETE FROM {sentTable} WHERE {keyPredicate};

DELETE FROM {completedTable} WHERE {keyPredicate};";



        await using var cmd = new SqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@WorkflowIdValue", workflowIdValue);

        cmd.Parameters.AddWithValue("@WorkflowTableKey", workflowTableKey);

        cmd.Parameters.AddWithValue("@WorkflowInstanceIdStr", workflowInstanceIdStr);

        cmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);

        cmd.Parameters.AddWithValue("@ActivityId", (object?)activityId ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Removes all mailbox rows for a workflow instance in one table (any activity).</summary>
    private static async Task DeleteMailboxRowsForInstanceAsync(
        SqlConnection connection,
        string workflowIdValue,
        string workflowTableKey,
        Guid workflowInstanceId,
        string workflowInstanceIdStr,
        string tableFull,
        CancellationToken cancellationToken)
    {
        const string instancePredicate = """
            (workflowId = @WorkflowIdValue OR workflowId = @WorkflowTableKey)
            AND (
                workflowInstanceId = @WorkflowInstanceIdStr
                OR TRY_CONVERT(UNIQUEIDENTIFIER, workflowInstanceId) = @WorkflowInstanceId
            )
            """;

        var sql = $"DELETE FROM {tableFull} WHERE {instancePredicate};";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@WorkflowIdValue", workflowIdValue);
        cmd.Parameters.AddWithValue("@WorkflowTableKey", workflowTableKey);
        cmd.Parameters.AddWithValue("@WorkflowInstanceIdStr", workflowInstanceIdStr);
        cmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string MailboxTable(string prefix, string tableSuffix) =>

        $"workflow.[{prefix}_{tableSuffix}]";

    private static async Task<MailboxExtraData> ResolveMailboxExtraDataAsync(
        SqlConnection connection,
        string suffix,
        Guid workflowId,
        Guid workflowInstanceId,
        MailboxFormSnapshot? formOverride,
        CancellationToken cancellationToken)
    {
        var attachmentTable = $"workflow.[WorkflowAttachments_{suffix}]";
        var processFormTable = $"workflow.[processForm_{suffix}]";

        Guid? repositoryId = null;
        Guid? itemId = null;
        string? formId = null;
        string? formEntryId = null;
        string? formData = null;

        var attachmentSql = $@"
SELECT TOP 1
    RepositoryId,
    ItemId,
    FormJsonId
FROM {attachmentTable}
WHERE WorkflowInstanceId = @WorkflowInstanceId
  AND IsDeleted = 0
ORDER BY ISNULL(ModifiedAtUtc, CreatedAtUtc) DESC, CreatedAtUtc DESC;";

        await using (var attachmentCmd = new SqlCommand(attachmentSql, connection))
        {
            attachmentCmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);
            await using var reader = await attachmentCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                repositoryId = ReadGuidOrNull(reader, 0);
                itemId = ReadGuidOrNull(reader, 1);
                formId = reader.IsDBNull(2) ? null : reader.GetString(2);
            }
        }

        var processFormSql = $@"
SELECT TOP 1
    WFormId,
    FormEntryId
FROM {processFormTable}
WHERE WorkflowInstanceId = @WorkflowInstanceId
  AND IsDeleted = 0
ORDER BY Id DESC;";

        await using (var processCmd = new SqlCommand(processFormSql, connection))
        {
            processCmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);
            await using var reader = await processCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                // processForm.WFormId is the authoritative form identifier for FormEntryId rows.
                formId = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
                formEntryId = reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1));
            }
        }

        if (string.IsNullOrWhiteSpace(formId))
            formId = await ResolveWorkflowFormIdAsync(connection, workflowId, cancellationToken);

        if (formOverride != null)
        {
            if (!string.IsNullOrWhiteSpace(formOverride.FormId))
                formId = formOverride.FormId.Trim();
            if (formOverride.FormEntryId is > 0)
                formEntryId = formOverride.FormEntryId.Value.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(formOverride.FormDataJson))
                formData = formOverride.FormDataJson;
        }

        if (string.IsNullOrWhiteSpace(formData)
            && !string.IsNullOrWhiteSpace(formId)
            && !string.IsNullOrWhiteSpace(formEntryId)
            && int.TryParse(formEntryId, out var entryId))
        {
            formData = await WorkflowEzfbFormDataLoader.LoadFormDataJsonAsync(
                connection, formId!, entryId, cancellationToken);
        }

        return new MailboxExtraData(repositoryId, itemId, formId, formEntryId, formData);
    }

    // LoadFormDataJsonAsync moved to WorkflowEzfbFormDataLoader (correct wFormId + ezfb column resolution).

    private static Guid? ReadGuidOrNull(SqlDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
            return null;

        var value = reader.GetValue(index);
        return value switch
        {
            Guid guid => guid,
            string text when Guid.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static async Task<string?> ResolveWorkflowFormIdAsync(
        SqlConnection connection,
        Guid workflowId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT FormId
FROM workflow.Workflows
WHERE Id = @WorkflowId AND IsDeleted = 0;
""";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? null : Convert.ToString(value)?.Trim();
    }

    public async Task PropagateInstanceFormDataAsync(
        Guid workflowId,
        Guid workflowInstanceId,
        MailboxFormSnapshot formData,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formData.FormId) || formData.FormEntryId is not > 0)
            return;

        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await _tableCreator.EnsureLegacyMailboxTablesAsync(workflowId, connectionString, cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var suffix = workflowId.ToString("N")[..8];
        var workflowIdValue = workflowId.ToString("D");
        var workflowIdCompact = workflowId.ToString("N");
        var instanceStr = workflowInstanceId.ToString("D");

        var formId = formData.FormId.Trim();
        var formEntryId = formData.FormEntryId.Value.ToString(CultureInfo.InvariantCulture);
        var formDataJson = formData.FormDataJson;

        if (string.IsNullOrWhiteSpace(formDataJson))
        {
            formDataJson = await WorkflowEzfbFormDataLoader.LoadFormDataJsonAsync(
                connection, formId, formData.FormEntryId.Value, cancellationToken);
        }

        foreach (var prefix in new[] { "Inbox", "Sent", "Completed" })
        {
            var table = MailboxTable(prefix, suffix);
            var sql = $@"
UPDATE {table}
SET formId = @FormId,
    formEntryId = @FormEntryId,
    formData = @FormData
WHERE (workflowId = @WorkflowIdValue OR workflowId = @WorkflowTableKey)
  AND (
      workflowInstanceId = @WorkflowInstanceIdStr
      OR TRY_CONVERT(UNIQUEIDENTIFIER, workflowInstanceId) = @WorkflowInstanceId
  );";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@FormId", formId);
            cmd.Parameters.AddWithValue("@FormEntryId", formEntryId);
            cmd.Parameters.AddWithValue("@FormData", (object?)formDataJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WorkflowIdValue", workflowIdValue);
            cmd.Parameters.AddWithValue("@WorkflowTableKey", workflowIdCompact);
            cmd.Parameters.AddWithValue("@WorkflowInstanceIdStr", instanceStr);
            cmd.Parameters.AddWithValue("@WorkflowInstanceId", workflowInstanceId);

            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (rows > 0)
            {
                _logger.LogDebug(
                    "Propagated formData to {Count} row(s) in {Table} for instance {InstanceId}.",
                    rows,
                    table,
                    workflowInstanceId);
            }
        }
    }

}

