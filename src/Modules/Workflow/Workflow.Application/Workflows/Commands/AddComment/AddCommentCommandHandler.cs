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

        // Get workflow instance to find workflow ID
        var instance = await _workflowRepository.GetInstanceByIdAsync(request.WorkflowInstanceId, cancellationToken);
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        // Add comment to workflow-specific table
        var commentId = Guid.NewGuid();
        await _dynamicTableRepository.AddCommentAsync(
            instance.WorkflowId,
            request.WorkflowInstanceId,
            request.Comments,
            userId,
            request.StepInstanceId,
            request.ExternalCommentsBy,
            request.ShowTo,
            cancellationToken);

        var tableName = _dynamicTableRepository.GetTableName(instance.WorkflowId, "WorkflowComments");

        return new AddCommentCommandResult(commentId, tableName);
    }
}
