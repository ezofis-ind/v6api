using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Commands.AddAttachment;

public sealed class AddAttachmentCommandHandler : IRequestHandler<AddAttachmentCommand, AddAttachmentCommandResult>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AddAttachmentCommandHandler(IWorkflowRepository workflowRepository, IDynamicTableRepository dynamicTableRepository, ICurrentUserProvider currentUserProvider)
    {
        _workflowRepository = workflowRepository;
        _dynamicTableRepository = dynamicTableRepository;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<AddAttachmentCommandResult> Handle(AddAttachmentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var instance = await _workflowRepository.GetInstanceByIdAsync(request.WorkflowInstanceId, cancellationToken);
        WorkflowInstanceScopeValidator.EnsureInstanceBelongsToWorkflow(instance, request.WorkflowId, request.WorkflowInstanceId);

        var attachmentId = await _dynamicTableRepository.AddAttachmentAsync(
            request.WorkflowId,
            request.WorkflowInstanceId,
            request.FileName,
            request.FilePath,
            userId,
            request.FileSize,
            request.ContentType,
            cancellationToken: cancellationToken);

        var tableName = _dynamicTableRepository.GetTableName(request.WorkflowId, "WorkflowAttachments");

        return new AddAttachmentCommandResult(attachmentId, request.WorkflowId, request.WorkflowInstanceId, tableName);
    }
}
