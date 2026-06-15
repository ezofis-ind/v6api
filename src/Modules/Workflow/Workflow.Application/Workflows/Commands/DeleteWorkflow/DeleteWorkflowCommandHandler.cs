using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Commands.DeleteWorkflow;

public sealed class DeleteWorkflowCommandHandler : IRequestHandler<DeleteWorkflowCommand, DeleteWorkflowCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteWorkflowCommandHandler(IWorkflowRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteWorkflowCommandResult> Handle(DeleteWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await _repository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow == null || workflow.IsDeleted)
            return new DeleteWorkflowCommandResult(false);

        workflow.SoftDelete();
        _repository.Delete(workflow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteWorkflowCommandResult(true);
    }
}
