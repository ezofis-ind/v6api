using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>Creates ML prediction models for workflows.</summary>
public sealed class WorkflowMlService : IWorkflowMlService
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<WorkflowMlService> _logger;

    public WorkflowMlService(
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        ILogger<WorkflowMlService> logger)
    {
        _tenantContext = tenantContext;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task CreateMlPredictionsAsync(
        Guid workflowId,
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

        // Filter blocks that have AI prediction settings
        var mlBlocks = blocks?
            .Where(b => b.Type != "START" && b.Type != "END")
            .Where(b => b.Settings.AiPrediction != null && 
                       b.Settings.AiPrediction.PredictionFields != null && 
                       b.Settings.AiPrediction.PredictionFields.Length > 0)
            .ToList();

        if (mlBlocks == null || mlBlocks.Count == 0)
            return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var block in mlBlocks)
        {
            // Build prediction columns
            var baseColumns = new[] { "levelId", "activityId", "activityUserId", "review" };
            var predictionFields = block.Settings?.AiPrediction?.PredictionFields ?? Array.Empty<string>();
            
            // TODO: Map predictionFields (JSON IDs) to actual form field names
            // This requires querying wFormControls table
            var predictionColumns = baseColumns.Concat(predictionFields ?? Array.Empty<string>()).ToArray();
            var conditionColumnsJson = JsonSerializer.Serialize(predictionColumns);

            var sql = @"
                INSERT INTO workflow.MlModelPrediction 
                (WorkflowId, ActivityId, HasModel, Remarks, TrainedCount, JsonTriggerCount, ConditionColumns, CreatedAtUtc, CreatedBy)
                VALUES (@WorkflowId, @ActivityId, 0, '', 0, 50, @ConditionColumns, @CreatedAt, @CreatedBy)";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@WorkflowId", workflowId);
            command.Parameters.AddWithValue("@ActivityId", block.Id);
            command.Parameters.AddWithValue("@ConditionColumns", conditionColumnsJson);
            command.Parameters.AddWithValue("@CreatedAt", currentTime);
            command.Parameters.AddWithValue("@CreatedBy", userId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("ML predictions created for workflow {WorkflowId}", workflowId);
    }
}

