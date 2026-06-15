using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.ListWorkflowInstances;

public sealed class ListWorkflowInstancesQueryHandler : IRequestHandler<ListWorkflowInstancesQuery, ListWorkflowInstancesQueryResult>
{
    private readonly IWorkflowRepository _repository;

    public ListWorkflowInstancesQueryHandler(IWorkflowRepository repository)
    {
        _repository = repository;
    }

    public async Task<ListWorkflowInstancesQueryResult> Handle(ListWorkflowInstancesQuery request, CancellationToken cancellationToken)
    {
        var instances = await _repository.ListInstancesAsync(request.WorkflowId, cancellationToken);
        var items = instances.Select(i => new WorkflowInstanceItem(i.Id, i.WorkflowId, i.WorkflowName, i.Status, i.CreatedAtUtc, i.StartedAtUtc, i.CompletedAtUtc, i.StartedBy)).ToList();
        return new ListWorkflowInstancesQueryResult(items);
    }
}
