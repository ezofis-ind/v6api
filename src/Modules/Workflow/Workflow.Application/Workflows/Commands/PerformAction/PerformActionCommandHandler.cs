using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.PerformAction;

public sealed class PerformActionCommandHandler : IRequestHandler<PerformActionCommand, PerformActionCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PerformActionCommandHandler(
        IWorkflowRepository repository,
        IDynamicTableRepository dynamicTableRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider)
    {
        _repository = repository;
        _dynamicTableRepository = dynamicTableRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PerformActionCommandResult> Handle(PerformActionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var instance = await _repository.GetInstanceByIdAsync(request.WorkflowInstanceId, cancellationToken);
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        // Get current running step
        var currentStep = instance.StepInstances
            .Where(s => s.Status == StepInstanceStatus.InProgress)
            .OrderBy(s => s.Order)
            .FirstOrDefault();

        string message;
        Guid? nextStepInstanceId = null;
        string? nextStepName = null;

        switch (request.Action)
        {
            case WorkflowAction.Approve:
                if (currentStep == null)
                    throw new InvalidOperationException("No running step to approve.");

                currentStep.Complete(userId);

                // Add approval comment
                var approvalComment = string.IsNullOrWhiteSpace(request.Comments)
                    ? "[APPROVED]"
                    : $"[APPROVED] {request.Comments}";

                await _dynamicTableRepository.AddCommentAsync(
                    instance.WorkflowId,
                    instance.Id,
                    approvalComment,
                    userId,
                    currentStep.Id,
                    cancellationToken: cancellationToken);

                // Move to next step if requested
                if (request.MoveToNextStep)
                {
                    var currentOrder = currentStep.Order;
                    var nextStep = instance.StepInstances
                        .Where(s => s.Order > currentOrder && s.Status == StepInstanceStatus.Pending)
                        .OrderBy(s => s.Order)
                        .FirstOrDefault();

                    if (nextStep != null)
                    {
                        nextStep.Start();
                        nextStepInstanceId = nextStep.Id;
                        nextStepName = nextStep.StepName;
                        message = $"Approved and moved to: {nextStep.StepName}";
                    }
                    else
                    {
                        instance.Complete(userId);
                        message = "Approved and workflow completed";
                    }
                }
                else
                {
                    message = "Step approved";
                }
                break;

            case WorkflowAction.Reject:
                if (currentStep == null)
                    throw new InvalidOperationException("No running step to reject.");

                currentStep.Cancel();

                // Add rejection comment
                var rejectionComment = string.IsNullOrWhiteSpace(request.Comments)
                    ? "[REJECTED]"
                    : $"[REJECTED] {request.Comments}";

                await _dynamicTableRepository.AddCommentAsync(
                    instance.WorkflowId,
                    instance.Id,
                    rejectionComment,
                    userId,
                    currentStep.Id,
                    cancellationToken: cancellationToken);

                instance.Cancel(userId);
                message = "Step rejected and workflow cancelled";
                break;

            case WorkflowAction.Hold:
                instance.Pause(userId);
                if (!string.IsNullOrWhiteSpace(request.Comments))
                {
                    await _dynamicTableRepository.AddCommentAsync(
                        instance.WorkflowId,
                        instance.Id,
                        $"[ON HOLD] {request.Comments}",
                        userId,
                        currentStep?.Id,
                        cancellationToken: cancellationToken);
                }
                message = "Workflow placed on hold";
                break;

            case WorkflowAction.Resume:
                instance.Resume(userId);
                if (!string.IsNullOrWhiteSpace(request.Comments))
                {
                    await _dynamicTableRepository.AddCommentAsync(
                        instance.WorkflowId,
                        instance.Id,
                        $"[RESUMED] {request.Comments}",
                        userId,
                        currentStep?.Id,
                        cancellationToken: cancellationToken);
                }
                message = "Workflow resumed";
                break;

            case WorkflowAction.Cancel:
                instance.Cancel(userId);
                if (currentStep != null)
                    currentStep.Cancel();

                if (!string.IsNullOrWhiteSpace(request.Comments))
                {
                    await _dynamicTableRepository.AddCommentAsync(
                        instance.WorkflowId,
                        instance.Id,
                        $"[CANCELLED] {request.Comments}",
                        userId,
                        currentStep?.Id,
                        cancellationToken: cancellationToken);
                }
                message = "Workflow cancelled";
                break;

            case WorkflowAction.Reassign:
                if (!request.AssignToUserId.HasValue)
                    throw new InvalidOperationException("AssignToUserId is required for reassignment.");

                instance.Reassign(request.AssignToUserId.Value);

                if (!string.IsNullOrWhiteSpace(request.Comments))
                {
                    await _dynamicTableRepository.AddCommentAsync(
                        instance.WorkflowId,
                        instance.Id,
                        $"[REASSIGNED] {request.Comments}",
                        userId,
                        currentStep?.Id,
                        cancellationToken: cancellationToken);
                }
                message = "Workflow reassigned";
                break;

            case WorkflowAction.RequestInfo:
                if (!string.IsNullOrWhiteSpace(request.Comments))
                {
                    await _dynamicTableRepository.AddCommentAsync(
                        instance.WorkflowId,
                        instance.Id,
                        $"[INFO REQUESTED] {request.Comments}",
                        userId,
                        currentStep?.Id,
                        cancellationToken: cancellationToken);
                }
                message = "Information requested";
                break;

            case WorkflowAction.Complete:
                if (currentStep != null)
                    currentStep.Complete(userId);

                instance.Complete(userId);

                if (!string.IsNullOrWhiteSpace(request.Comments))
                {
                    await _dynamicTableRepository.AddCommentAsync(
                        instance.WorkflowId,
                        instance.Id,
                        $"[COMPLETED] {request.Comments}",
                        userId,
                        currentStep?.Id,
                        cancellationToken: cancellationToken);
                }
                message = "Workflow completed";
                break;

            default:
                throw new InvalidOperationException($"Unknown action: {request.Action}");
        }

        await _repository.UpdateInstanceAsync(instance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PerformActionCommandResult(
            true,
            message,
            instance.Status.ToString(),
            nextStepInstanceId,
            nextStepName
        );
    }
}
