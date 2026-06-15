using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Entities;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.ApproveStep;

public sealed class ApproveStepCommandHandler : IRequestHandler<ApproveStepCommand, ApproveStepCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITenantContext _tenantContext;

    public ApproveStepCommandHandler(
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

    public async Task<ApproveStepCommandResult> Handle(ApproveStepCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var instance = await _repository.GetInstanceByIdAsync(request.WorkflowInstanceId, cancellationToken);
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        var stepInstance = instance.StepInstances.FirstOrDefault(s => s.Id == request.StepInstanceId);
        if (stepInstance == null)
            throw new InvalidOperationException("Step instance not found.");

        if (stepInstance.Status == StepInstanceStatus.Completed)
            throw new InvalidOperationException("Step is already completed.");

        // Insert WorkflowApproval record for Approval steps (audit trail) - before completing
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
            approval.Approve(userId, request.Comments);
            await _repository.AddApprovalAsync(approval, cancellationToken);
        }

        // Determine if we should complete step and move: depends on ApprovalPolicy for Approval steps
        bool shouldCompleteAndMove = request.MoveToNextStep;
        if (stepInstance.StepType == StepType.Approval && request.MoveToNextStep)
        {
            var workflow = await _repository.GetByIdWithStepsAsync(instance.WorkflowId, cancellationToken);
            var stepDef = workflow?.Steps.FirstOrDefault(s => s.Id == stepInstance.WorkflowStepId);
            if (stepDef != null)
            {
                var approvedCount = await _repository.CountApprovedForStepInstanceAsync(stepInstance.Id, cancellationToken);
                var totalAfterThisApproval = approvedCount + 1; // +1 for the approval we just added (not yet saved)
                var requiredCount = Math.Max(1, stepDef.GetApproverIds().Count);
                shouldCompleteAndMove = stepDef.ApprovalPolicy switch
                {
                    ApprovalPolicy.AnyOneApprove => true,
                    ApprovalPolicy.AllMustApprove => totalAfterThisApproval >= requiredCount,
                    _ => true
                };
            }
        }

        if (shouldCompleteAndMove)
            stepInstance.Complete(userId);

        // Add approval comment if provided
        if (!string.IsNullOrWhiteSpace(request.Comments))
        {
            await _dynamicTableRepository.AddCommentAsync(
                instance.WorkflowId,
                instance.Id,
                $"[APPROVED] {request.Comments}",
                userId,
                stepInstance.Id,
                cancellationToken: cancellationToken);
        }

        Guid? nextStepInstanceId = null;
        string? nextStepName = null;

        // Move to next step if we completed and requested
        if (shouldCompleteAndMove && request.MoveToNextStep)
        {
            var currentOrder = stepInstance.Order;
            var nextStep = instance.StepInstances
                .Where(s => s.Order > currentOrder && s.Status == StepInstanceStatus.Pending)
                .OrderBy(s => s.Order)
                .FirstOrDefault();

            if (nextStep != null)
            {
                nextStep.Start();
                nextStepInstanceId = nextStep.Id;
                nextStepName = nextStep.StepName;
            }
            else
            {
                // No more steps - complete the workflow
                instance.Complete(userId);
            }
        }

        await _repository.UpdateInstanceAsync(instance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var message = nextStepInstanceId.HasValue
            ? $"Step approved and moved to: {nextStepName}"
            : instance.Status == WorkflowInstanceStatus.Completed
                ? "Step approved and workflow completed"
                : shouldCompleteAndMove
                    ? "Step approved"
                    : "Approval recorded; waiting for other approvers.";

        return new ApproveStepCommandResult(
            true,
            message,
            nextStepInstanceId,
            stepInstance.StepName,
            nextStepName
        );
    }
}
