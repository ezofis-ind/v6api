using System.Text.Json;
using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetWorkflowById;

public sealed class GetWorkflowByIdQueryHandler : IRequestHandler<GetWorkflowByIdQuery, GetWorkflowByIdQueryResult?>
{
    private readonly IWorkflowRepository _repository;
    private readonly IWorkflowJsonStorageService _jsonStorage;

    public GetWorkflowByIdQueryHandler(
        IWorkflowRepository repository,
        IWorkflowJsonStorageService jsonStorage)
    {
        _repository = repository;
        _jsonStorage = jsonStorage;
    }

    public async Task<GetWorkflowByIdQueryResult?> Handle(GetWorkflowByIdQuery request, CancellationToken cancellationToken)
    {
        var workflow = await _repository.GetByIdWithStepsAsync(request.WorkflowId, cancellationToken);
        if (workflow == null || workflow.IsDeleted)
            return null;

        var steps = workflow.Steps
            .Select(s => new WorkflowStepItem(
                s.Id, s.Name, s.Description, s.StepType, s.Order, s.IsRequired,
                s.AssignedToUserId, s.AssignedToRole, s.ActivityId, s.StageType))
            .ToList();

        var workflowJson = await LoadWorkflowJsonElementAsync(request.WorkflowId, cancellationToken);
        workflowJson = WorkflowJsonDbSyncHelper.ApplyDbMetadata(
            workflowJson,
            workflow.Name,
            workflow.Description,
            workflow.Status);

        return new GetWorkflowByIdQueryResult(
            workflow.Id,
            workflow.Name,
            workflow.Description,
            workflow.Status,
            workflow.TriggerType,
            workflow.TriggerConfig,
            workflow.Version,
            workflow.CreatedAtUtc,
            steps,
            workflowJson,
            workflow.RepositoryId,
            workflow.FormId);
    }

    private async Task<JsonElement?> LoadWorkflowJsonElementAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var json = await _jsonStorage.GetWorkflowJsonAsync(workflowId, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
