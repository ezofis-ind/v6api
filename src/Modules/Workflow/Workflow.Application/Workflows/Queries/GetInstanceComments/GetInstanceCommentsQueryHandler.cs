using MediatR;
using SaaSApp.Workflow.Application.Contracts;

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
        // Get workflow instance to find workflow ID
        var instance = await _workflowRepository.GetInstanceByIdAsync(request.InstanceId, cancellationToken);
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        // Get comments from workflow-specific table
        var comments = await _dynamicTableRepository.GetCommentsAsync(instance.WorkflowId, request.InstanceId, cancellationToken);

        var commentItems = comments.Select(c => new CommentItem(
            (Guid)c.Id,
            (Guid)c.WorkflowInstanceId,
            c.StepInstanceId,
            (string)c.Comments,
            c.ExternalCommentsBy,
            (int)c.ShowTo,
            c.EmbedJson,
            (bool)c.EmbedStatus,
            (DateTime)c.CreatedAtUtc,
            (Guid)c.CreatedBy
        )).ToList();

        var tableName = _dynamicTableRepository.GetTableName(instance.WorkflowId, "WorkflowComments");

        return new GetInstanceCommentsQueryResult(commentItems, tableName);
    }
}
