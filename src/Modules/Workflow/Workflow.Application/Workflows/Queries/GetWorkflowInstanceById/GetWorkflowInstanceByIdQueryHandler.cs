using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetWorkflowInstanceById;

public sealed class GetWorkflowInstanceByIdQueryHandler : IRequestHandler<GetWorkflowInstanceByIdQuery, GetWorkflowInstanceByIdQueryResult?>
{
    private readonly IWorkflowRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetWorkflowInstanceByIdQueryHandler(IWorkflowRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<GetWorkflowInstanceByIdQueryResult?> Handle(GetWorkflowInstanceByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return null;

        var instance = await _repository.GetInstanceByIdAsync(request.InstanceId, cancellationToken);
        if (instance == null || instance.TenantId != tenantId.Value)
            return null;

        var stepItems = instance.StepInstances
            .OrderBy(s => s.Order)
            .Select(s => new WorkflowStepInstanceItem(
                s.Id,
                s.StepName,
                s.StepType,
                s.Order,
                s.Status,
                s.StartedAtUtc,
                s.CompletedAtUtc))
            .ToList();

        return new GetWorkflowInstanceByIdQueryResult(
            instance.Id,
            instance.WorkflowId,
            instance.WorkflowName,
            instance.Status,
            instance.CurrentStepInstanceId,
            instance.CreatedAtUtc,
            instance.StartedAtUtc,
            instance.CompletedAtUtc,
            instance.StartedBy,
            stepItems);
    }
}
