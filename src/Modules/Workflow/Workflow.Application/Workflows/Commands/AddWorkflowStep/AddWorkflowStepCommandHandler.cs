using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Entities;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.AddWorkflowStep;

public sealed class AddWorkflowStepCommandHandler : IRequestHandler<AddWorkflowStepCommand, AddWorkflowStepCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public AddWorkflowStepCommandHandler(IWorkflowRepository repository, IUnitOfWork unitOfWork, ITenantContext tenantContext)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<AddWorkflowStepCommandResult> Handle(AddWorkflowStepCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

        var workflow = await _repository.GetByIdWithStepsAsync(request.WorkflowId, cancellationToken);
        if (workflow == null || workflow.IsDeleted || workflow.TenantId != tenantId)
            return new AddWorkflowStepCommandResult(Guid.Empty, false);

        if (workflow.Status != WorkflowStatus.Draft)
            throw new InvalidOperationException("Steps can only be added to workflows in Draft status.");

        var step = WorkflowStep.Create(
            request.WorkflowId,
            request.Name,
            request.StepType,
            request.Order,
            request.Description,
            request.Config,
            request.IsRequired,
            request.AssignedToUserId,
            request.AssignedToRole,
            request.ApprovedNextStepId,
            request.RejectedNextStepId,
            request.ApprovalPolicy,
            request.ApproversJson,
            request.ActivityId);

        workflow.AddStep(step);
        await _repository.AddStepAsync(step, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddWorkflowStepCommandResult(step.Id, true);
    }
}
