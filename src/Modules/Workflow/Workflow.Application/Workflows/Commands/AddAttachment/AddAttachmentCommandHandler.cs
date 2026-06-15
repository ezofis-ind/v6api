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

        // Get workflow instance to find workflow ID
        var instance = await _workflowRepository.GetInstanceByIdAsync(request.WorkflowInstanceId, cancellationToken);
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        var attachmentId = await _dynamicTableRepository.AddAttachmentAsync(
            instance.WorkflowId,
            request.WorkflowInstanceId,
            request.FileName,
            request.FilePath,
            userId,
            request.FileSize,
            request.ContentType,
            cancellationToken: cancellationToken);

        var tableName = _dynamicTableRepository.GetTableName(instance.WorkflowId, "WorkflowAttachments");

        return new AddAttachmentCommandResult(attachmentId, tableName);
    }
}
