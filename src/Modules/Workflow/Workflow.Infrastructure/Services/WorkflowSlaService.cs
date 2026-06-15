using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>Creates SLA rules for workflows.</summary>
public sealed class WorkflowSlaService : IWorkflowSlaService
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<WorkflowSlaService> _logger;

    public WorkflowSlaService(
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        ILogger<WorkflowSlaService> logger)
    {
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task CreateSlaRulesAsync(
        Guid workflowId,
        List<WorkflowSlaRuleDto>? generalSlaRules,
        List<WorkflowBlockDto> blocks,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _tenantContext.ConnectionString;
        if (string.IsNullOrEmpty(connectionString))
            return;

        var userId = _currentUserProvider.GetUserId();
        if (userId == null)
            return;

        var currentTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var workflowIdStr = workflowId.ToString("N").Substring(0, 8);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Create slaResponse table if needed
        var checkTableSql = $@"
            SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = 'workflow' AND TABLE_NAME = 'slaResponse_{workflowIdStr}'";

        await using var checkCommand = new SqlCommand(checkTableSql, connection);
        var tableExists = await checkCommand.ExecuteScalarAsync(cancellationToken) != null;

        if (!tableExists)
        {
            var createTableSql = $@"
                CREATE TABLE workflow.slaResponse_{workflowIdStr} (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ActivityId NVARCHAR(500) NULL,
                    Name NVARCHAR(500) NULL,
                    Duration NVARCHAR(500) NULL,
                    DurationType NVARCHAR(500) NULL,
                    Level INT NULL,
                    SettingsJson NVARCHAR(MAX) NULL,
                    MasterFormId NVARCHAR(500) NULL,
                    FieldId NVARCHAR(500) NULL,
                    CreatedAtUtc NVARCHAR(50) NULL,
                    ModifiedAtUtc NVARCHAR(50) NULL,
                    CreatedBy UNIQUEIDENTIFIER NOT NULL,
                    ModifiedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0
                )";

            await using var createCommand = new SqlCommand(createTableSql, connection);
            await createCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert block-level SLA rules
        if (blocks != null)
        {
            foreach (var block in blocks)
            {
                if (block.Settings.SlaRules != null && block.Settings.SlaRules.Count > 0)
                {
                    foreach (var sla in block.Settings.SlaRules)
                    {
                        if (sla.Id == 0) // New SLA rule
                        {
                            var insertSql = $@"
                                INSERT INTO workflow.slaResponse_{workflowIdStr} 
                                (ActivityId, Name, Duration, DurationType, Level, SettingsJson, MasterFormId, FieldId, CreatedAtUtc, CreatedBy, ModifiedAtUtc, ModifiedBy, IsDeleted)
                                VALUES (@ActivityId, @Name, @Duration, @DurationType, @Level, @SettingsJson, @MasterFormId, @FieldId, @CreatedAt, @CreatedBy, '', @CreatedBy, 0)";

                            await using var insertCommand = new SqlCommand(insertSql, connection);
                            insertCommand.Parameters.AddWithValue("@ActivityId", block.Id);
                            insertCommand.Parameters.AddWithValue("@Name", sla.Name ?? "");
                            insertCommand.Parameters.AddWithValue("@Duration", sla.Duration?.ToString() ?? "");
                            insertCommand.Parameters.AddWithValue("@DurationType", sla.DurationType ?? "");
                            insertCommand.Parameters.AddWithValue("@Level", sla.Level ?? 0);
                            insertCommand.Parameters.AddWithValue("@SettingsJson", sla.SettingsJson ?? "");
                            insertCommand.Parameters.AddWithValue("@MasterFormId", sla.MasterFormId ?? "");
                            insertCommand.Parameters.AddWithValue("@FieldId", sla.FieldId ?? "");
                            insertCommand.Parameters.AddWithValue("@CreatedAt", currentTime);
                            insertCommand.Parameters.AddWithValue("@CreatedBy", userId.Value);
                            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }
                }
            }
        }

        // Insert general SLA rules (slaResolution table)
        if (generalSlaRules != null && generalSlaRules.Count > 0)
        {
            // Create slaResolution table if needed
            var checkResTableSql = $@"
                SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = 'workflow' AND TABLE_NAME = 'slaResolution_{workflowIdStr}'";

            await using var checkResCommand = new SqlCommand(checkResTableSql, connection);
            var resTableExists = await checkResCommand.ExecuteScalarAsync(cancellationToken) != null;

            if (!resTableExists)
            {
                var createResTableSql = $@"
                    CREATE TABLE workflow.slaResolution_{workflowIdStr} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(500) NULL,
                        Duration NVARCHAR(500) NULL,
                        DurationType NVARCHAR(500) NULL,
                        Level INT NULL,
                        Users NVARCHAR(500) NULL,
                        Action NVARCHAR(500) NULL,
                        MasterFormId NVARCHAR(500) NULL,
                        FieldId NVARCHAR(500) NULL,
                        CreatedAtUtc NVARCHAR(50) NULL,
                        ModifiedAtUtc NVARCHAR(50) NULL,
                        CreatedBy UNIQUEIDENTIFIER NOT NULL,
                        ModifiedBy UNIQUEIDENTIFIER NULL,
                        IsDeleted BIT NOT NULL DEFAULT 0
                    )";

                await using var createResCommand = new SqlCommand(createResTableSql, connection);
                await createResCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var sla in generalSlaRules)
            {
                if (sla.Id == 0)
                {
                    var usersJson = sla.Users != null ? JsonSerializer.Serialize(sla.Users) : "0";

                    var insertSql = $@"
                        INSERT INTO workflow.slaResolution_{workflowIdStr} 
                        (Name, Duration, DurationType, Level, Users, Action, MasterFormId, FieldId, CreatedAtUtc, CreatedBy, ModifiedAtUtc, ModifiedBy, IsDeleted)
                        VALUES (@Name, @Duration, @DurationType, @Level, @Users, @Action, @MasterFormId, @FieldId, @CreatedAt, @CreatedBy, '', @CreatedBy, 0)";

                    await using var insertCommand = new SqlCommand(insertSql, connection);
                    insertCommand.Parameters.AddWithValue("@Name", sla.Name ?? "");
                    insertCommand.Parameters.AddWithValue("@Duration", sla.Duration?.ToString() ?? "");
                    insertCommand.Parameters.AddWithValue("@DurationType", sla.DurationType ?? "");
                    insertCommand.Parameters.AddWithValue("@Level", sla.Level ?? 0);
                    insertCommand.Parameters.AddWithValue("@Users", usersJson);
                    insertCommand.Parameters.AddWithValue("@Action", sla.Action ?? "");
                    insertCommand.Parameters.AddWithValue("@MasterFormId", sla.MasterFormId ?? "");
                    insertCommand.Parameters.AddWithValue("@FieldId", sla.FieldId ?? "");
                    insertCommand.Parameters.AddWithValue("@CreatedAt", currentTime);
                    insertCommand.Parameters.AddWithValue("@CreatedBy", userId.Value);
                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        _logger.LogInformation("SLA rules created for workflow {WorkflowId}", workflowId);
    }
}

