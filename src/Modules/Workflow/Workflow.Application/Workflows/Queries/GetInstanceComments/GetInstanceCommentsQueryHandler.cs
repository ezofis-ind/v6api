using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetInstanceComments;

public sealed class GetInstanceCommentsQueryHandler : IRequestHandler<GetInstanceCommentsQuery, GetInstanceCommentsQueryResult>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IDynamicTableRepository _dynamicTableRepository;

    public GetInstanceCommentsQueryHandler(IWorkflowRepository workflowRepository, IDynamicTableRepository dynamicTableRepository)
    {
        _workflowRepository = workflowRepository;
        _dynamicTableRepository = dynamicTableRepository;
    }

    public async Task<GetInstanceCommentsQueryResult> Handle(GetInstanceCommentsQuery request, CancellationToken cancellationToken)
    {
        var instance = await _workflowRepository.GetInstanceByIdAsync(request.InstanceId, cancellationToken);
        WorkflowInstanceScopeValidator.EnsureInstanceBelongsToWorkflow(instance, request.WorkflowId, request.InstanceId);

        var comments = await _dynamicTableRepository.GetCommentsAsync(request.WorkflowId, request.InstanceId, cancellationToken);

        var commentItems = comments
            .Select(c => new CommentItem(
            c.Id,
            request.WorkflowId,
            c.WorkflowInstanceId,
            c.StepInstanceId,
            c.Comments,
            c.ExternalCommentsBy,
            c.ShowTo,
            c.EmbedJson,
            c.EmbedStatus,
            c.CreatedAtUtc,
            c.CreatedBy
        )).ToList();

        var tableName = _dynamicTableRepository.GetTableName(request.WorkflowId, "WorkflowComments");

        return new GetInstanceCommentsQueryResult(request.WorkflowId, request.InstanceId, commentItems, tableName);
    }
}
