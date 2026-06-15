using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Entities;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.RejectStep;

public sealed class RejectStepCommandHandler : IRequestHandler<RejectStepCommand, RejectStepCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITenantContext _tenantContext;

    public RejectStepCommandHandler(
        IWorkflowRepository repository,
        IDynamicTableRepository dynamicTableRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dynamicTableRepository = dynamicTableRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _tenantContext = tenantContext;
    }

    public async Task<RejectStepCommandResult> Handle(RejectStepCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var instance = await _repository.GetInstanceByIdAsync(request.WorkflowInstanceId, cancellationToken);
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        var stepInstance = instance.StepInstances.FirstOrDefault(s => s.Id == request.StepInstanceId);
        if (stepInstance == null)
            throw new InvalidOperationException("Step instance not found.");

        if (stepInstance.Status == StepInstanceStatus.Completed)
            throw new InvalidOperationException("Cannot reject a completed step.");

        // Insert WorkflowApproval record for Approval steps (audit trail)
        if (stepInstance.StepType == StepType.Approval)
        {
            var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
            var requestedBy = instance.StepInstances
                .Where(s => s.Order < stepInstance.Order && s.Status == StepInstanceStatus.Completed)
                .OrderByDescending(s => s.Order)
                .Select(s => s.CompletedBy)
                .FirstOrDefault() ?? instance.StartedBy;
            var approval = WorkflowApproval.Create(
                tenantId,
                instance.Id,
                stepInstance.Id,
                requestedBy,
                stepInstance.AssignedToUserId,
                stepInstance.AssignedToRole);
            approval.Reject(userId, request.Reason);
            await _repository.AddApprovalAsync(approval, cancellationToken);
        }

        // Add rejection comment
        await _dynamicTableRepository.AddCommentAsync(
            instance.WorkflowId,
            instance.Id,
            $"[REJECTED] {request.Reason}",
            userId,
            stepInstance.Id,
            cancellationToken: cancellationToken);

        string message;
        string workflowStatus;

        if (request.CancelWorkflow)
        {
            // Cancel the entire workflow
            instance.Cancel(userId);
            stepInstance.Cancel();
            message = $"Step rejected and workflow cancelled. Reason: {request.Reason}";
            workflowStatus = "Cancelled";
        }
        else if (request.ReassignToUserId.HasValue)
        {
            // Reassign to another user
            instance.Reassign(request.ReassignToUserId.Value);
            stepInstance.Cancel();
            message = $"Step rejected and workflow reassigned. Reason: {request.Reason}";
            workflowStatus = "Reassigned";
        }
        else
        {
            // Just mark step as rejected (workflow stays in current state)
            stepInstance.Cancel();
            message = $"Step rejected. Reason: {request.Reason}";
            workflowStatus = instance.Status.ToString();
        }

        await _repository.UpdateInstanceAsync(instance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RejectStepCommandResult(true, message, workflowStatus);
    }
}
