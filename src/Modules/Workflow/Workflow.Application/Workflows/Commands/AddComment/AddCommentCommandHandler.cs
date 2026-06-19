using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Commands.AddComment;

public sealed class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, AddCommentCommandResult>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AddCommentCommandHandler(IWorkflowRepository workflowRepository, IDynamicTableRepository dynamicTableRepository, ICurrentUserProvider currentUserProvider)
    {
        _workflowRepository = workflowRepository;
        _dynamicTableRepository = dynamicTableRepository;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<AddCommentCommandResult> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var instance = await _workflowRepository.GetInstanceByIdAsync(request.WorkflowInstanceId, cancellationToken);
        WorkflowInstanceScopeValidator.EnsureInstanceBelongsToWorkflow(instance, request.WorkflowId, request.WorkflowInstanceId);

        var commentId = await _dynamicTableRepository.AddCommentAsync(
            request.WorkflowId,
            request.WorkflowInstanceId,
            request.Comments,
            userId,
            request.StepInstanceId,
            request.ExternalCommentsBy,
            request.ShowTo,
            cancellationToken);

        var tableName = _dynamicTableRepository.GetTableName(request.WorkflowId, "WorkflowComments");

        return new AddCommentCommandResult(commentId, request.WorkflowId, request.WorkflowInstanceId, tableName);
    }
}
