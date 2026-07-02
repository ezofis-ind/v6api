using System.Reflection;
using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Entities;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Infrastructure.Persistence;

/// <summary>
/// Persists workflow instances to per-workflow tables (WorkflowInstances_{suffix}, WorkflowStepInstances_{suffix}).
/// Uses WorkflowInstanceLookup for cross-workflow queries.
/// </summary>
public sealed class WorkflowInstanceStore : IWorkflowInstanceStore
{
    private readonly ITenantContext _tenantContext;

    public WorkflowInstanceStore(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    private static string GetSuffix(Guid workflowId) => workflowId.ToString("N")[..8];
    private static string InstancesTable(Guid workflowId) => $"workflow.WorkflowInstances_{GetSuffix(workflowId)}";
    private static string StepInstancesTable(Guid workflowId) => $"workflow.WorkflowStepInstances_{GetSuffix(workflowId)}";
    private static string InstanceSlasTable(Guid workflowId) => $"workflow.WorkflowInstanceSlas_{GetSuffix(workflowId)}";

    public async Task AddAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var connStr = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string required.");
        var suffix = GetSuffix(instance.WorkflowId);
        var instancesTable = InstancesTable(instance.WorkflowId);
        var stepInstancesTable = StepInstancesTable(instance.WorkflowId);

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        await EnsureStepInstanceColumnsAsync(conn, suffix, cancellationToken);

        // Insert instance
        var instanceSql = $@"
            INSERT INTO {instancesTable} (
                Id, TenantId, WorkflowId, WorkflowName, WorkflowVersion, Status, CurrentStepInstanceId,
                CreatedAtUtc, StartedAtUtc, CompletedAtUtc, StartedBy, Context, ErrorMessage,
                ReferenceNumber, CustomerName, CustomerEmail, CustomerPhone, Department, Category, Priority,
                Tags, CustomFieldsJson, AssignedToUserId, AssignedToGroupId, LastActivityAtUtc,
                ViewCount, IsArchived, ArchivedAtUtc, SourceType, SourceId, LastViewedAtUtc, LastViewedBy)
            VALUES (
                @Id, @TenantId, @WorkflowId, @WorkflowName, @WorkflowVersion, @Status, @CurrentStepInstanceId,
                @CreatedAtUtc, @StartedAtUtc, @CompletedAtUtc, @StartedBy, @Context, @ErrorMessage,
                @ReferenceNumber, @CustomerName, @CustomerEmail, @CustomerPhone, @Department, @Category, @Priority,
                @Tags, @CustomFieldsJson, @AssignedToUserId, @AssignedToGroupId, @LastActivityAtUtc,
                @ViewCount, @IsArchived, @ArchivedAtUtc, @SourceType, @SourceId, @LastViewedAtUtc, @LastViewedBy)";

        await using (var cmd = new SqlCommand(instanceSql, conn))
        {
            AddInstanceParams(cmd, instance);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert step instances
        foreach (var step in instance.StepInstances)
        {
            var stepSql = $@"
                INSERT INTO {stepInstancesTable} (
                    Id, WorkflowInstanceId, WorkflowStepId, StepName, StepType, [Order], Status,
                    AssignedToUserId, AssignedToRole, CreatedAtUtc, StartedAtUtc, CompletedAtUtc,
                    CompletedBy, Result, ErrorMessage, ActivityId, StageType)
                VALUES (
                    @Id, @WorkflowInstanceId, @WorkflowStepId, @StepName, @StepType, @Order, @Status,
                    @AssignedToUserId, @AssignedToRole, @CreatedAtUtc, @StartedAtUtc, @CompletedAtUtc,
                    @CompletedBy, @Result, @ErrorMessage, @ActivityId, @StageType)";
            await using var stepCmd = new SqlCommand(stepSql, conn);
            AddStepInstanceParams(stepCmd, step, instance.Id);
            await stepCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert SLA if present
        if (instance.Sla != null)
        {
            var slaTable = InstanceSlasTable(instance.WorkflowId);
            var slaSql = $@"
                INSERT INTO {slaTable} (
                    Id, WorkflowInstanceId, Priority, ResponseDeadline, ResolutionDeadline, EscalationDeadline,
                    ResponseAchievedAt, ResolutionAchievedAt, ResponseStatus, ResolutionStatus, IsEscalated, EscalatedAt, CreatedAtUtc)
                VALUES (
                    @Id, @WorkflowInstanceId, @Priority, @ResponseDeadline, @ResolutionDeadline, @EscalationDeadline,
                    @ResponseAchievedAt, @ResolutionAchievedAt, @ResponseStatus, @ResolutionStatus, @IsEscalated, @EscalatedAt, @CreatedAtUtc)";
            await using var slaCmd = new SqlCommand(slaSql, conn);
            AddSlaParams(slaCmd, instance.Sla);
            await slaCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert lookup row
        var lookupSql = @"
            INSERT INTO workflow.WorkflowInstanceLookup (
                InstanceId, WorkflowId, TenantId, WorkflowName, Status, AssignedToUserId, StartedBy,
                CreatedAtUtc, LastActivityAtUtc, CompletedAtUtc, IsArchived, Priority, CurrentStepInstanceId,
                SlaPriority, ResponseStatus, ResolutionStatus, ResponseDeadline, ResolutionDeadline, IsEscalated)
            VALUES (
                @InstanceId, @WorkflowId, @TenantId, @WorkflowName, @Status, @AssignedToUserId, @StartedBy,
                @CreatedAtUtc, @LastActivityAtUtc, @CompletedAtUtc, @IsArchived, @Priority, @CurrentStepInstanceId,
                @SlaPriority, @ResponseStatus, @ResolutionStatus, @ResponseDeadline, @ResolutionDeadline, @IsEscalated)";
        await using var lookupCmd = new SqlCommand(lookupSql, conn);
        AddLookupParams(lookupCmd, instance);
        await lookupCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<WorkflowInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var connStr = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string required.");

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        // Lookup workflowId
        var lookupSql = "SELECT WorkflowId FROM workflow.WorkflowInstanceLookup WHERE InstanceId = @Id";
        await using var lookupCmd = new SqlCommand(lookupSql, conn);
        lookupCmd.Parameters.AddWithValue("@Id", instanceId);
        var workflowIdObj = await lookupCmd.ExecuteScalarAsync(cancellationToken);
        if (workflowIdObj == null || workflowIdObj == DBNull.Value)
            return null;

        var workflowId = (Guid)workflowIdObj;
        var instancesTable = InstancesTable(workflowId);
        var stepInstancesTable = StepInstancesTable(workflowId);
        var slaTable = InstanceSlasTable(workflowId);

        // Check if per-workflow tables exist (workflow may have been published)
        var tableCheck = $@"
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstances_{GetSuffix(workflowId)}' AND schema_id = SCHEMA_ID('workflow'))
                SELECT 1";
        await using var checkCmd = new SqlCommand(tableCheck, conn);
        var exists = await checkCmd.ExecuteScalarAsync(cancellationToken);
        if (exists == null)
            return null;

        // Load instance
        var instanceSql = $"SELECT * FROM {instancesTable} WHERE Id = @Id";
        await using var instanceCmd = new SqlCommand(instanceSql, conn);
        instanceCmd.Parameters.AddWithValue("@Id", instanceId);

        WorkflowInstance? instance = null;
        await using (var reader = await instanceCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                instance = ReadWorkflowInstance(reader, workflowId);
        }

        if (instance == null)
            return null;

        // Load step instances
        var stepSql = $"SELECT * FROM {stepInstancesTable} WHERE WorkflowInstanceId = @InstanceId ORDER BY [Order]";
        await using var stepCmd = new SqlCommand(stepSql, conn);
        stepCmd.Parameters.AddWithValue("@InstanceId", instanceId);
        await using (var stepReader = await stepCmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await stepReader.ReadAsync(cancellationToken))
            {
                var step = ReadWorkflowStepInstance(stepReader, instanceId);
                instance.AddStepInstance(step);
            }
        }

        // Load SLA (table may not exist for workflows without SLA)
        try
        {
            var slaSql = $"SELECT * FROM {slaTable} WHERE WorkflowInstanceId = @InstanceId";
            await using var slaCmd = new SqlCommand(slaSql, conn);
            slaCmd.Parameters.AddWithValue("@InstanceId", instanceId);
            await using var slaReader = await slaCmd.ExecuteReaderAsync(cancellationToken);
            if (await slaReader.ReadAsync(cancellationToken))
            {
                var sla = ReadWorkflowInstanceSla(slaReader, instanceId);
                instance.SetSla(sla);
            }
        }
        catch (SqlException) { /* Table may not exist */ }

        return instance;
    }

    public async Task<IReadOnlyList<WorkflowInstance>> ListByWorkflowIdAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var connStr = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string required.");
        var instancesTable = InstancesTable(workflowId);
        var stepInstancesTable = StepInstancesTable(workflowId);

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        var tableCheck = $@"
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstances_{GetSuffix(workflowId)}' AND schema_id = SCHEMA_ID('workflow'))
                SELECT 1";
        await using var checkCmd = new SqlCommand(tableCheck, conn);
        if (await checkCmd.ExecuteScalarAsync(cancellationToken) == null)
            return Array.Empty<WorkflowInstance>();

        var sql = $"SELECT * FROM {instancesTable} WHERE WorkflowId = @WorkflowId ORDER BY CreatedAtUtc DESC";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WorkflowId", workflowId);

        var list = new List<WorkflowInstance>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var instance = ReadWorkflowInstance(reader, workflowId);
                list.Add(instance);
            }
        }

        foreach (var instance in list)
        {
            var stepSql = $"SELECT * FROM {stepInstancesTable} WHERE WorkflowInstanceId = @InstanceId ORDER BY [Order]";
            await using var stepCmd = new SqlCommand(stepSql, conn);
            stepCmd.Parameters.AddWithValue("@InstanceId", instance.Id);
            await using var stepReader = await stepCmd.ExecuteReaderAsync(cancellationToken);
            while (await stepReader.ReadAsync(cancellationToken))
            {
                var step = ReadWorkflowStepInstance(stepReader, instance.Id);
                instance.AddStepInstance(step);
            }
        }

        return list;
    }

    public async Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var connStr = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string required.");
        var instancesTable = InstancesTable(instance.WorkflowId);

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        var sql = $@"
            UPDATE {instancesTable} SET
                Status = @Status, CurrentStepInstanceId = @CurrentStepInstanceId,
                StartedAtUtc = @StartedAtUtc, CompletedAtUtc = @CompletedAtUtc,
                ErrorMessage = @ErrorMessage, AssignedToUserId = @AssignedToUserId,
                LastActivityAtUtc = @LastActivityAtUtc, ViewCount = @ViewCount,
                IsArchived = @IsArchived, ArchivedAtUtc = @ArchivedAtUtc
            WHERE Id = @Id";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", instance.Id);
        cmd.Parameters.AddWithValue("@Status", (int)instance.Status);
        cmd.Parameters.AddWithValue("@CurrentStepInstanceId", (object?)instance.CurrentStepInstanceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StartedAtUtc", (object?)instance.StartedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAtUtc", (object?)instance.CompletedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)instance.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AssignedToUserId", (object?)instance.AssignedToUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastActivityAtUtc", (object?)instance.LastActivityAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ViewCount", instance.ViewCount);
        cmd.Parameters.AddWithValue("@IsArchived", instance.IsArchived);
        cmd.Parameters.AddWithValue("@ArchivedAtUtc", (object?)instance.ArchivedAtUtc ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        // Update step instances
        var stepInstancesTable = StepInstancesTable(instance.WorkflowId);
        foreach (var step in instance.StepInstances)
        {
            var stepSql = $@"
                UPDATE {stepInstancesTable} SET
                    Status = @Status, StartedAtUtc = @StartedAtUtc, CompletedAtUtc = @CompletedAtUtc,
                    CompletedBy = @CompletedBy, Result = @Result, ErrorMessage = @ErrorMessage
                WHERE Id = @Id";
            await using var stepCmd = new SqlCommand(stepSql, conn);
            stepCmd.Parameters.AddWithValue("@Id", step.Id);
            stepCmd.Parameters.AddWithValue("@Status", (int)step.Status);
            stepCmd.Parameters.AddWithValue("@StartedAtUtc", (object?)step.StartedAtUtc ?? DBNull.Value);
            stepCmd.Parameters.AddWithValue("@CompletedAtUtc", (object?)step.CompletedAtUtc ?? DBNull.Value);
            stepCmd.Parameters.AddWithValue("@CompletedBy", (object?)step.CompletedBy ?? DBNull.Value);
            stepCmd.Parameters.AddWithValue("@Result", (object?)step.Result ?? DBNull.Value);
            stepCmd.Parameters.AddWithValue("@ErrorMessage", (object?)step.ErrorMessage ?? DBNull.Value);
            await stepCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Update SLA if present
        if (instance.Sla != null)
        {
            try
            {
                var slaTable = InstanceSlasTable(instance.WorkflowId);
                var slaSql = $@"
                    UPDATE {slaTable} SET
                        ResponseAchievedAt = @ResponseAchievedAt, ResolutionAchievedAt = @ResolutionAchievedAt,
                        ResponseStatus = @ResponseStatus, ResolutionStatus = @ResolutionStatus,
                        IsEscalated = @IsEscalated, EscalatedAt = @EscalatedAt
                    WHERE WorkflowInstanceId = @WorkflowInstanceId";
                await using var slaCmd = new SqlCommand(slaSql, conn);
                slaCmd.Parameters.AddWithValue("@WorkflowInstanceId", instance.Id);
                slaCmd.Parameters.AddWithValue("@ResponseAchievedAt", (object?)instance.Sla.ResponseAchievedAt ?? DBNull.Value);
                slaCmd.Parameters.AddWithValue("@ResolutionAchievedAt", (object?)instance.Sla.ResolutionAchievedAt ?? DBNull.Value);
                slaCmd.Parameters.AddWithValue("@ResponseStatus", (int)instance.Sla.ResponseStatus);
                slaCmd.Parameters.AddWithValue("@ResolutionStatus", (int)instance.Sla.ResolutionStatus);
                slaCmd.Parameters.AddWithValue("@IsEscalated", instance.Sla.IsEscalated);
                slaCmd.Parameters.AddWithValue("@EscalatedAt", (object?)instance.Sla.EscalatedAt ?? DBNull.Value);
                await slaCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException) { /* Table may not exist */ }
        }

        // Update lookup
        var lookupSql = @"
            UPDATE workflow.WorkflowInstanceLookup SET
                Status = @Status, AssignedToUserId = @AssignedToUserId, LastActivityAtUtc = @LastActivityAtUtc,
                CompletedAtUtc = @CompletedAtUtc, IsArchived = @IsArchived, CurrentStepInstanceId = @CurrentStepInstanceId,
                SlaPriority = @SlaPriority, ResponseStatus = @ResponseStatus, ResolutionStatus = @ResolutionStatus,
                ResponseDeadline = @ResponseDeadline, ResolutionDeadline = @ResolutionDeadline, IsEscalated = @IsEscalated
            WHERE InstanceId = @InstanceId";
        await using var lookupCmd = new SqlCommand(lookupSql, conn);
        lookupCmd.Parameters.AddWithValue("@InstanceId", instance.Id);
        lookupCmd.Parameters.AddWithValue("@Status", (int)instance.Status);
        lookupCmd.Parameters.AddWithValue("@AssignedToUserId", (object?)instance.AssignedToUserId ?? DBNull.Value);
        lookupCmd.Parameters.AddWithValue("@LastActivityAtUtc", (object?)instance.LastActivityAtUtc ?? DBNull.Value);
        lookupCmd.Parameters.AddWithValue("@CompletedAtUtc", (object?)instance.CompletedAtUtc ?? DBNull.Value);
        lookupCmd.Parameters.AddWithValue("@IsArchived", instance.IsArchived);
        lookupCmd.Parameters.AddWithValue("@CurrentStepInstanceId", (object?)instance.CurrentStepInstanceId ?? DBNull.Value);
        AddLookupSlaParams(lookupCmd, instance);
        await lookupCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(List<WorkflowInstance> Items, int TotalCount)> GetMyInboxAsync(Guid userId, int pageNumber, int pageSize, Guid? workflowId = null, CancellationToken cancellationToken = default)
    {
        var whereClause = "AssignedToUserId = @UserId AND Status IN (0, 1) AND IsArchived = 0";
        var parameters = new List<SqlParameter> { new SqlParameter("@UserId", userId) };
        if (workflowId.HasValue)
        {
            whereClause += " AND WorkflowId = @WorkflowId";
            parameters.Add(new SqlParameter("@WorkflowId", workflowId.Value));
        }
        return await GetInstancesFromLookupAsync(
            whereClause,
            parameters.ToArray(),
            "Priority DESC, ISNULL(LastActivityAtUtc, CreatedAtUtc) DESC",
            pageNumber, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowInboxCount>> GetWorkflowWiseInboxCountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connStr = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string required.");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        var sql = @"
            SELECT WorkflowId, WorkflowName, COUNT(*) AS InboxCount
            FROM workflow.WorkflowInstanceLookup
            WHERE AssignedToUserId = @UserId AND Status IN (0, 1) AND IsArchived = 0
            GROUP BY WorkflowId, WorkflowName
            ORDER BY InboxCount DESC, WorkflowName";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        var list = new List<WorkflowInboxCount>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new WorkflowInboxCount(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2)));
        }
        return list;
    }

    public async Task<(List<WorkflowInstance> Items, int TotalCount)> GetMySentAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await GetInstancesFromLookupAsync(
            "StartedBy = @UserId AND IsArchived = 0",
            new[] { new SqlParameter("@UserId", userId) },
            "CreatedAtUtc DESC",
            pageNumber, pageSize, cancellationToken);
    }

    public async Task<(List<WorkflowInstance> Items, int TotalCount)> GetMyCompletedAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await GetInstancesFromLookupAsync(
            "(StartedBy = @UserId OR AssignedToUserId = @UserId) AND Status = 3",
            new[] { new SqlParameter("@UserId", userId) },
            "LastActivityAtUtc DESC",
            pageNumber, pageSize, cancellationToken);
    }

    public async Task<WorkflowCounts> GetWorkflowCountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connStr = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string required.");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        var sql = @"
            SELECT
                (SELECT COUNT(*) FROM workflow.WorkflowInstanceLookup WHERE AssignedToUserId = @UserId AND Status IN (0, 1) AND IsArchived = 0),
                (SELECT COUNT(*) FROM workflow.WorkflowInstanceLookup WHERE StartedBy = @UserId AND IsArchived = 0),
                (SELECT COUNT(*) FROM workflow.WorkflowInstanceLookup WHERE (StartedBy = @UserId OR AssignedToUserId = @UserId) AND Status = 3),
                (SELECT COUNT(*) FROM workflow.WorkflowInstanceLookup WHERE Status NOT IN (3, 5) AND IsArchived = 0)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new WorkflowCounts(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
    }

    public async Task<IReadOnlyList<SlaBreachInfo>> ListSlaBreachesAsync(CancellationToken cancellationToken = default)
    {
        var connStr = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string required.");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        var sql = @"
            SELECT InstanceId, WorkflowId, WorkflowName, Status, SlaPriority, ResponseStatus, ResolutionStatus,
                   ResponseDeadline, ResolutionDeadline, IsEscalated, CreatedAtUtc
            FROM workflow.WorkflowInstanceLookup
            WHERE (ResponseStatus IN (1, 2) OR ResolutionStatus IN (1, 2))
              AND SlaPriority IS NOT NULL
            ORDER BY SlaPriority DESC, ResolutionDeadline";
        await using var cmd = new SqlCommand(sql, conn);
        var list = new List<SlaBreachInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new SlaBreachInfo(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                (WorkflowInstanceStatus)reader.GetInt32(3), (SlaPriority)reader.GetInt32(4),
                (SlaStatus)reader.GetInt32(5), (SlaStatus)reader.GetInt32(6),
                reader.GetDateTime(7), reader.GetDateTime(8), reader.GetBoolean(9), reader.GetDateTime(10)));
        }
        return list;
    }

    private async Task<(List<WorkflowInstance> Items, int TotalCount)> GetInstancesFromLookupAsync(
        string whereClause, SqlParameter[] parameters, string orderBy, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var connStr = _tenantContext.ConnectionString ?? throw new InvalidOperationException("Connection string required.");
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        var countSql = $"SELECT COUNT(*) FROM workflow.WorkflowInstanceLookup WHERE {whereClause}";
        await using var countCmd = new SqlCommand(countSql, conn);
        foreach (var p in parameters)
            countCmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
        var totalCount = (int)(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0);

        var offset = (pageNumber - 1) * pageSize;
        var dataSql = $@"
            SELECT InstanceId, WorkflowId FROM workflow.WorkflowInstanceLookup
            WHERE {whereClause}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        await using var dataCmd = new SqlCommand(dataSql, conn);
        foreach (var p in parameters)
            dataCmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
        dataCmd.Parameters.AddWithValue("@Offset", offset);
        dataCmd.Parameters.AddWithValue("@PageSize", pageSize);

        var items = new List<WorkflowInstance>();
        var pairs = new List<(Guid InstanceId, Guid WorkflowId)>();
        await using (var reader = await dataCmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                pairs.Add((reader.GetGuid(0), reader.GetGuid(1)));
        }

        foreach (var group in pairs.GroupBy(p => p.WorkflowId))
        {
            var loaded = await LoadInstancesBatchAsync(
                conn,
                group.Key,
                group.Select(p => p.InstanceId).ToList(),
                cancellationToken);
            items.AddRange(loaded);
        }

        var order = pairs.Select((p, i) => (p.InstanceId, i)).ToDictionary(x => x.InstanceId, x => x.i);
        items.Sort((a, b) => order.GetValueOrDefault(a.Id, int.MaxValue).CompareTo(order.GetValueOrDefault(b.Id, int.MaxValue)));
        return (items, totalCount);
    }

    private async Task<List<WorkflowInstance>> LoadInstancesBatchAsync(
        SqlConnection conn,
        Guid workflowId,
        IReadOnlyList<Guid> instanceIds,
        CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
            return [];

        var instancesTable = InstancesTable(workflowId);
        var stepInstancesTable = StepInstancesTable(workflowId);
        var slaTable = InstanceSlasTable(workflowId);

        var tableCheck = $@"
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstances_{GetSuffix(workflowId)}' AND schema_id = SCHEMA_ID('workflow'))
                SELECT 1";
        await using (var checkCmd = new SqlCommand(tableCheck, conn))
        {
            if (await checkCmd.ExecuteScalarAsync(cancellationToken) == null)
                return [];
        }

        var idParams = new List<string>(instanceIds.Count);
        var instanceSql = new System.Text.StringBuilder($"SELECT * FROM {instancesTable} WHERE Id IN (");
        for (var i = 0; i < instanceIds.Count; i++)
        {
            var param = $"@Id{i}";
            idParams.Add(param);
            if (i > 0)
                instanceSql.Append(", ");
            instanceSql.Append(param);
        }
        instanceSql.Append(')');

        var instances = new Dictionary<Guid, WorkflowInstance>(instanceIds.Count);
        await using (var instanceCmd = new SqlCommand(instanceSql.ToString(), conn))
        {
            for (var i = 0; i < instanceIds.Count; i++)
                instanceCmd.Parameters.AddWithValue($"@Id{i}", instanceIds[i]);

            await using var reader = await instanceCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var instance = ReadWorkflowInstance(reader, workflowId);
                instances[instance.Id] = instance;
            }
        }

        if (instances.Count == 0)
            return [];

        var stepSql = new System.Text.StringBuilder($"SELECT * FROM {stepInstancesTable} WHERE WorkflowInstanceId IN (");
        for (var i = 0; i < instanceIds.Count; i++)
        {
            if (i > 0)
                stepSql.Append(", ");
            stepSql.Append(idParams[i]);
        }
        stepSql.Append(") ORDER BY [Order]");

        await using (var stepCmd = new SqlCommand(stepSql.ToString(), conn))
        {
            for (var i = 0; i < instanceIds.Count; i++)
                stepCmd.Parameters.AddWithValue($"@Id{i}", instanceIds[i]);

            await using var stepReader = await stepCmd.ExecuteReaderAsync(cancellationToken);
            while (await stepReader.ReadAsync(cancellationToken))
            {
                var instanceId = stepReader.GetGuid(stepReader.GetOrdinal("WorkflowInstanceId"));
                if (instances.TryGetValue(instanceId, out var instance))
                {
                    var step = ReadWorkflowStepInstance(stepReader, instanceId);
                    instance.AddStepInstance(step);
                }
            }
        }

        try
        {
            var slaSql = new System.Text.StringBuilder($"SELECT * FROM {slaTable} WHERE WorkflowInstanceId IN (");
            for (var i = 0; i < instanceIds.Count; i++)
            {
                if (i > 0)
                    slaSql.Append(", ");
                slaSql.Append(idParams[i]);
            }
            slaSql.Append(')');

            await using var slaCmd = new SqlCommand(slaSql.ToString(), conn);
            for (var i = 0; i < instanceIds.Count; i++)
                slaCmd.Parameters.AddWithValue($"@Id{i}", instanceIds[i]);

            await using var slaReader = await slaCmd.ExecuteReaderAsync(cancellationToken);
            while (await slaReader.ReadAsync(cancellationToken))
            {
                var instanceId = slaReader.GetGuid(slaReader.GetOrdinal("WorkflowInstanceId"));
                if (instances.TryGetValue(instanceId, out var instance))
                {
                    var sla = ReadWorkflowInstanceSla(slaReader, instanceId);
                    instance.SetSla(sla);
                }
            }
        }
        catch (SqlException) { /* SLA table may not exist */ }

        return instanceIds
            .Where(instances.ContainsKey)
            .Select(id => instances[id])
            .ToList();
    }

    private static void AddInstanceParams(SqlCommand cmd, WorkflowInstance i)
    {
        cmd.Parameters.AddWithValue("@Id", i.Id);
        cmd.Parameters.AddWithValue("@TenantId", i.TenantId);
        cmd.Parameters.AddWithValue("@WorkflowId", i.WorkflowId);
        cmd.Parameters.AddWithValue("@WorkflowName", i.WorkflowName);
        cmd.Parameters.AddWithValue("@WorkflowVersion", i.WorkflowVersion);
        cmd.Parameters.AddWithValue("@Status", (int)i.Status);
        cmd.Parameters.AddWithValue("@CurrentStepInstanceId", (object?)i.CurrentStepInstanceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAtUtc", i.CreatedAtUtc);
        cmd.Parameters.AddWithValue("@StartedAtUtc", (object?)i.StartedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAtUtc", (object?)i.CompletedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StartedBy", i.StartedBy);
        cmd.Parameters.AddWithValue("@Context", (object?)i.Context ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)i.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReferenceNumber", (object?)i.ReferenceNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CustomerName", (object?)i.CustomerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CustomerEmail", (object?)i.CustomerEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CustomerPhone", (object?)i.CustomerPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Department", (object?)i.Department ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Category", (object?)i.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Priority", i.Priority);
        cmd.Parameters.AddWithValue("@Tags", (object?)i.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CustomFieldsJson", (object?)i.CustomFieldsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AssignedToUserId", (object?)i.AssignedToUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AssignedToGroupId", (object?)i.AssignedToGroupId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastActivityAtUtc", (object?)i.LastActivityAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ViewCount", i.ViewCount);
        cmd.Parameters.AddWithValue("@IsArchived", i.IsArchived);
        cmd.Parameters.AddWithValue("@ArchivedAtUtc", (object?)i.ArchivedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SourceType", (object?)i.SourceType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SourceId", (object?)i.SourceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastViewedAtUtc", DBNull.Value);
        cmd.Parameters.AddWithValue("@LastViewedBy", DBNull.Value);
    }

    private static void AddStepInstanceParams(SqlCommand cmd, WorkflowStepInstance s, Guid instanceId)
    {
        cmd.Parameters.AddWithValue("@Id", s.Id);
        cmd.Parameters.AddWithValue("@WorkflowInstanceId", instanceId);
        cmd.Parameters.AddWithValue("@WorkflowStepId", s.WorkflowStepId);
        cmd.Parameters.AddWithValue("@StepName", s.StepName);
        cmd.Parameters.AddWithValue("@StepType", (int)s.StepType);
        cmd.Parameters.AddWithValue("@Order", s.Order);
        cmd.Parameters.AddWithValue("@Status", (int)s.Status);
        cmd.Parameters.AddWithValue("@AssignedToUserId", (object?)s.AssignedToUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AssignedToRole", (object?)s.AssignedToRole ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAtUtc", s.CreatedAtUtc);
        cmd.Parameters.AddWithValue("@StartedAtUtc", (object?)s.StartedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAtUtc", (object?)s.CompletedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedBy", (object?)s.CompletedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Result", (object?)s.Result ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)s.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ActivityId", (object?)s.ActivityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StageType", (object?)s.StageType ?? DBNull.Value);
    }

    private static void AddSlaParams(SqlCommand cmd, WorkflowInstanceSla s)
    {
        cmd.Parameters.AddWithValue("@Id", s.Id);
        cmd.Parameters.AddWithValue("@WorkflowInstanceId", s.WorkflowInstanceId);
        cmd.Parameters.AddWithValue("@Priority", (int)s.Priority);
        cmd.Parameters.AddWithValue("@ResponseDeadline", s.ResponseDeadline);
        cmd.Parameters.AddWithValue("@ResolutionDeadline", s.ResolutionDeadline);
        cmd.Parameters.AddWithValue("@EscalationDeadline", (object?)s.EscalationDeadline ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ResponseAchievedAt", (object?)s.ResponseAchievedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ResolutionAchievedAt", (object?)s.ResolutionAchievedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ResponseStatus", (int)s.ResponseStatus);
        cmd.Parameters.AddWithValue("@ResolutionStatus", (int)s.ResolutionStatus);
        cmd.Parameters.AddWithValue("@IsEscalated", s.IsEscalated);
        cmd.Parameters.AddWithValue("@EscalatedAt", (object?)s.EscalatedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAtUtc", s.CreatedAtUtc);
    }

    private static void AddLookupParams(SqlCommand cmd, WorkflowInstance i)
    {
        cmd.Parameters.AddWithValue("@InstanceId", i.Id);
        cmd.Parameters.AddWithValue("@WorkflowId", i.WorkflowId);
        cmd.Parameters.AddWithValue("@TenantId", i.TenantId);
        cmd.Parameters.AddWithValue("@WorkflowName", i.WorkflowName);
        cmd.Parameters.AddWithValue("@Status", (int)i.Status);
        cmd.Parameters.AddWithValue("@AssignedToUserId", (object?)i.AssignedToUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StartedBy", i.StartedBy);
        cmd.Parameters.AddWithValue("@CreatedAtUtc", i.CreatedAtUtc);
        cmd.Parameters.AddWithValue("@LastActivityAtUtc", (object?)i.LastActivityAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAtUtc", (object?)i.CompletedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsArchived", i.IsArchived);
        cmd.Parameters.AddWithValue("@Priority", i.Priority);
        cmd.Parameters.AddWithValue("@CurrentStepInstanceId", (object?)i.CurrentStepInstanceId ?? DBNull.Value);
        AddLookupSlaParams(cmd, i);
    }

    private static void AddLookupSlaParams(SqlCommand cmd, WorkflowInstance i)
    {
        if (i.Sla != null)
        {
            cmd.Parameters.AddWithValue("@SlaPriority", (int)i.Sla.Priority);
            cmd.Parameters.AddWithValue("@ResponseStatus", (int)i.Sla.ResponseStatus);
            cmd.Parameters.AddWithValue("@ResolutionStatus", (int)i.Sla.ResolutionStatus);
            cmd.Parameters.AddWithValue("@ResponseDeadline", i.Sla.ResponseDeadline);
            cmd.Parameters.AddWithValue("@ResolutionDeadline", i.Sla.ResolutionDeadline);
            cmd.Parameters.AddWithValue("@IsEscalated", i.Sla.IsEscalated);
        }
        else
        {
            cmd.Parameters.AddWithValue("@SlaPriority", DBNull.Value);
            cmd.Parameters.AddWithValue("@ResponseStatus", DBNull.Value);
            cmd.Parameters.AddWithValue("@ResolutionStatus", DBNull.Value);
            cmd.Parameters.AddWithValue("@ResponseDeadline", DBNull.Value);
            cmd.Parameters.AddWithValue("@ResolutionDeadline", DBNull.Value);
            cmd.Parameters.AddWithValue("@IsEscalated", false);
        }
    }

    private static WorkflowInstance ReadWorkflowInstance(SqlDataReader r, Guid workflowId)
    {
        var instance = (WorkflowInstance)Activator.CreateInstance(typeof(WorkflowInstance), nonPublic: true)!;
        SetProperty(instance, "Id", r.GetGuid(r.GetOrdinal("Id")));
        SetProperty(instance, "TenantId", r.GetGuid(r.GetOrdinal("TenantId")));
        SetProperty(instance, "WorkflowId", workflowId);
        SetProperty(instance, "WorkflowName", r.GetString(r.GetOrdinal("WorkflowName")));
        SetProperty(instance, "WorkflowVersion", r.GetInt32(r.GetOrdinal("WorkflowVersion")));
        SetProperty(instance, "Status", (WorkflowInstanceStatus)r.GetInt32(r.GetOrdinal("Status")));
        SetProperty(instance, "CurrentStepInstanceId", GetGuidOrNull(r, "CurrentStepInstanceId"));
        SetProperty(instance, "CreatedAtUtc", r.GetDateTime(r.GetOrdinal("CreatedAtUtc")));
        SetProperty(instance, "StartedAtUtc", GetDateTimeOrNull(r, "StartedAtUtc"));
        SetProperty(instance, "CompletedAtUtc", GetDateTimeOrNull(r, "CompletedAtUtc"));
        SetProperty(instance, "StartedBy", r.GetGuid(r.GetOrdinal("StartedBy")));
        SetProperty(instance, "Context", GetStringOrNull(r, "Context"));
        SetProperty(instance, "ErrorMessage", GetStringOrNull(r, "ErrorMessage"));
        SetProperty(instance, "ReferenceNumber", GetStringOrNull(r, "ReferenceNumber"));
        SetProperty(instance, "CustomerName", GetStringOrNull(r, "CustomerName"));
        SetProperty(instance, "CustomerEmail", GetStringOrNull(r, "CustomerEmail"));
        SetProperty(instance, "CustomerPhone", GetStringOrNull(r, "CustomerPhone"));
        SetProperty(instance, "Department", GetStringOrNull(r, "Department"));
        SetProperty(instance, "Category", GetStringOrNull(r, "Category"));
        SetProperty(instance, "Priority", r.GetInt32(r.GetOrdinal("Priority")));
        SetProperty(instance, "Tags", GetStringOrNull(r, "Tags"));
        SetProperty(instance, "CustomFieldsJson", GetStringOrNull(r, "CustomFieldsJson"));
        SetProperty(instance, "AssignedToUserId", GetGuidOrNull(r, "AssignedToUserId"));
        SetProperty(instance, "AssignedToGroupId", GetGuidOrNull(r, "AssignedToGroupId"));
        SetProperty(instance, "LastActivityAtUtc", GetDateTimeOrNull(r, "LastActivityAtUtc"));
        SetProperty(instance, "ViewCount", r.GetInt32(r.GetOrdinal("ViewCount")));
        SetProperty(instance, "IsArchived", r.GetBoolean(r.GetOrdinal("IsArchived")));
        SetProperty(instance, "ArchivedAtUtc", GetDateTimeOrNull(r, "ArchivedAtUtc"));
        SetProperty(instance, "SourceType", GetStringOrNull(r, "SourceType"));
        SetProperty(instance, "SourceId", GetStringOrNull(r, "SourceId"));
        return instance;
    }

    private static WorkflowStepInstance ReadWorkflowStepInstance(SqlDataReader r, Guid instanceId)
    {
        var step = (WorkflowStepInstance)Activator.CreateInstance(typeof(WorkflowStepInstance), nonPublic: true)!;
        SetProperty(step, "Id", r.GetGuid(r.GetOrdinal("Id")));
        SetProperty(step, "WorkflowInstanceId", instanceId);
        SetProperty(step, "WorkflowStepId", r.GetGuid(r.GetOrdinal("WorkflowStepId")));
        SetProperty(step, "StepName", r.GetString(r.GetOrdinal("StepName")));
        SetProperty(step, "StepType", (StepType)r.GetInt32(r.GetOrdinal("StepType")));
        SetProperty(step, "Order", r.GetInt32(r.GetOrdinal("Order")));
        SetProperty(step, "Status", (StepInstanceStatus)r.GetInt32(r.GetOrdinal("Status")));
        SetProperty(step, "AssignedToUserId", GetGuidOrNull(r, "AssignedToUserId"));
        SetProperty(step, "AssignedToRole", GetStringOrNull(r, "AssignedToRole"));
        SetProperty(step, "CreatedAtUtc", r.GetDateTime(r.GetOrdinal("CreatedAtUtc")));
        SetProperty(step, "StartedAtUtc", GetDateTimeOrNull(r, "StartedAtUtc"));
        SetProperty(step, "CompletedAtUtc", GetDateTimeOrNull(r, "CompletedAtUtc"));
        SetProperty(step, "CompletedBy", GetGuidOrNull(r, "CompletedBy"));
        SetProperty(step, "Result", GetStringOrNull(r, "Result"));
        SetProperty(step, "ErrorMessage", GetStringOrNull(r, "ErrorMessage"));
        if (HasColumn(r, "ActivityId"))
            SetProperty(step, "ActivityId", GetStringOrNull(r, "ActivityId"));
        if (HasColumn(r, "StageType"))
            SetProperty(step, "StageType", GetStringOrNull(r, "StageType"));
        return step;
    }

    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static WorkflowInstanceSla ReadWorkflowInstanceSla(SqlDataReader r, Guid instanceId)
    {
        var sla = (WorkflowInstanceSla)Activator.CreateInstance(typeof(WorkflowInstanceSla), nonPublic: true)!;
        SetProperty(sla, "Id", r.GetGuid(r.GetOrdinal("Id")));
        SetProperty(sla, "WorkflowInstanceId", instanceId);
        SetProperty(sla, "Priority", (SlaPriority)r.GetInt32(r.GetOrdinal("Priority")));
        SetProperty(sla, "ResponseDeadline", r.GetDateTime(r.GetOrdinal("ResponseDeadline")));
        SetProperty(sla, "ResolutionDeadline", r.GetDateTime(r.GetOrdinal("ResolutionDeadline")));
        SetProperty(sla, "EscalationDeadline", GetDateTimeOrNull(r, "EscalationDeadline"));
        SetProperty(sla, "ResponseAchievedAt", GetDateTimeOrNull(r, "ResponseAchievedAt"));
        SetProperty(sla, "ResolutionAchievedAt", GetDateTimeOrNull(r, "ResolutionAchievedAt"));
        SetProperty(sla, "ResponseStatus", (SlaStatus)r.GetInt32(r.GetOrdinal("ResponseStatus")));
        SetProperty(sla, "ResolutionStatus", (SlaStatus)r.GetInt32(r.GetOrdinal("ResolutionStatus")));
        SetProperty(sla, "IsEscalated", r.GetBoolean(r.GetOrdinal("IsEscalated")));
        SetProperty(sla, "EscalatedAt", GetDateTimeOrNull(r, "EscalatedAt"));
        SetProperty(sla, "CreatedAtUtc", r.GetDateTime(r.GetOrdinal("CreatedAtUtc")));
        return sla;
    }

    private static void SetProperty(object obj, string name, object? value)
    {
        var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(obj, value);
    }

    private static Guid? GetGuidOrNull(SqlDataReader r, string name)
    {
        var idx = r.GetOrdinal(name);
        return r.IsDBNull(idx) ? null : r.GetGuid(idx);
    }

    private static DateTime? GetDateTimeOrNull(SqlDataReader r, string name)
    {
        var idx = r.GetOrdinal(name);
        return r.IsDBNull(idx) ? null : r.GetDateTime(idx);
    }

    private static string? GetStringOrNull(SqlDataReader r, string name)
    {
        var idx = r.GetOrdinal(name);
        return r.IsDBNull(idx) ? null : r.GetString(idx);
    }

    /// <summary>Add ActivityId/StageType to per-workflow step instance tables created before this column existed.</summary>
    private static async Task EnsureStepInstanceColumnsAsync(
        SqlConnection connection,
        string suffix,
        CancellationToken cancellationToken)
    {
        var sql = $@"
IF OBJECT_ID(N'workflow.WorkflowStepInstances_{suffix}', N'U') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'workflow.WorkflowStepInstances_{suffix}') AND name = N'ActivityId')
BEGIN
    ALTER TABLE workflow.WorkflowStepInstances_{suffix} ADD ActivityId NVARCHAR(128) NULL;
END
IF OBJECT_ID(N'workflow.WorkflowStepInstances_{suffix}', N'U') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'workflow.WorkflowStepInstances_{suffix}') AND name = N'StageType')
BEGIN
    ALTER TABLE workflow.WorkflowStepInstances_{suffix} ADD StageType NVARCHAR(64) NULL;
END";

        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
