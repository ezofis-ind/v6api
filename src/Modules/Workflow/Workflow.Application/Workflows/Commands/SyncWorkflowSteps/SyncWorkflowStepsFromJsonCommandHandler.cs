using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Commands.SyncWorkflowSteps;

public sealed class SyncWorkflowStepsFromJsonCommandHandler
    : IRequestHandler<SyncWorkflowStepsFromJsonCommand, SyncWorkflowStepsFromJsonCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IWorkflowStepSyncService _stepSyncService;

    public SyncWorkflowStepsFromJsonCommandHandler(
        IWorkflowRepository repository,
        IWorkflowStepSyncService stepSyncService)
    {
        _repository = repository;
        _stepSyncService = stepSyncService;
    }

    public async Task<SyncWorkflowStepsFromJsonCommandResult> Handle(
        SyncWorkflowStepsFromJsonCommand request,
        CancellationToken cancellationToken)
    {
        var workflow = await _repository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow == null || workflow.IsDeleted)
            return new SyncWorkflowStepsFromJsonCommandResult(false, 0);

        await _stepSyncService.SyncStepsFromWorkflowJsonAsync(request.WorkflowId, null, cancellationToken);

        var withSteps = await _repository.GetByIdWithStepsAsync(request.WorkflowId, cancellationToken);
        var count = withSteps?.Steps.Count ?? 0;

        return new SyncWorkflowStepsFromJsonCommandResult(true, count);
    }
}
