using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows;
using SaaSApp.Workflow.Domain.Entities;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Application.Workflows.Commands.MoveToNextStep;

public sealed class MoveToNextStepCommandHandler : IRequestHandler<MoveToNextStepCommand, MoveToNextStepCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly IWorkflowLegacyTransactionSyncService _legacyTransactionSync;
    private readonly IWorkflowLegacyMailboxSyncService _mailboxSync;
    private readonly IWorkflowApAgentMoveNextService _apAgentMoveNext;
    private readonly IWorkflowEzfbFormDataLoader _ezfbFormDataLoader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public MoveToNextStepCommandHandler(
        IWorkflowRepository repository,
        IDynamicTableRepository dynamicTableRepository,
        IWorkflowLegacyTransactionSyncService legacyTransactionSync,
        IWorkflowLegacyMailboxSyncService mailboxSync,
        IWorkflowApAgentMoveNextService apAgentMoveNext,
        IWorkflowEzfbFormDataLoader ezfbFormDataLoader,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider)
    {
        _repository = repository;
        _dynamicTableRepository = dynamicTableRepository;
        _legacyTransactionSync = legacyTransactionSync;
        _mailboxSync = mailboxSync;
        _apAgentMoveNext = apAgentMoveNext;
        _ezfbFormDataLoader = ezfbFormDataLoader;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<MoveToNextStepCommandResult> Handle(MoveToNextStepCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ActivityId))
            throw new ArgumentException("activityId is required.");

        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");

        var instance = await _repository.GetInstanceByIdAsync(request.WorkflowInstanceId, cancellationToken);
        if (instance == null)
            throw new InvalidOperationException("Workflow instance not found.");

        if (instance.Status == WorkflowInstanceStatus.Completed)
        {
            var legacyFlowStatus = await _legacyTransactionSync.GetLegacyProcessFlowStatusAsync(
                instance.WorkflowId,
                instance.Id,
                instance.ReferenceNumber,
                cancellationToken);

            // Allow move-next when legacy process is still running (FlowStatus = 0), e.g. after manual reset for testing.
            if (legacyFlowStatus == 0)
            {
                instance.Reopen(userId);
                await _repository.UpdateInstanceAsync(instance, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
                throw new InvalidOperationException("Workflow is already completed.");
        }

        if (instance.Status == WorkflowInstanceStatus.Cancelled)
            throw new InvalidOperationException("Workflow is cancelled.");

        var lineItemsJson = request.FormLineItemsJson;

        if (!string.IsNullOrWhiteSpace(request.FormId)
            && request.FormEntryId is > 0
            && ((request.FormDataFields is { Count: > 0 })
                || !string.IsNullOrWhiteSpace(lineItemsJson)))
        {
            await _apAgentMoveNext.ApplyFormDataToEzfbAsync(
                request.FormId,
                request.FormEntryId.Value,
                request.FormDataFields ?? new Dictionary<string, string>(),
                lineItemsJson,
                cancellationToken);
        }

        var workflow = await _repository.GetByIdWithStepsAsync(instance.WorkflowId, cancellationToken);
        if (workflow == null || workflow.IsDeleted)
            throw new InvalidOperationException("Workflow not found.");

        var orderedSteps = workflow.Steps.OrderBy(s => s.Order).ToList();
        if (orderedSteps.Count == 0)
            throw new InvalidOperationException("Workflow has no steps defined.");

        var targetDefinitionStep = ResolveStepByActivityId(orderedSteps, request.ActivityId);
        if (targetDefinitionStep == null)
            throw new InvalidOperationException($"No workflow step found for activityId '{request.ActivityId.Trim()}'.");

        var isApAgentStep = WorkflowStepTransitionHelper.IsApAgentStep(targetDefinitionStep);
        var routesByAction = WorkflowStepActionsHelper.HasMatchingAction(targetDefinitionStep, request.Review)
            || WorkflowStepTransitionHelper.IsApproveReview(request.Review);

        if (isApAgentStep && !string.IsNullOrWhiteSpace(request.Review) && !routesByAction)
        {
            if (request.ApAgent != null)
            {
                await _apAgentMoveNext.SaveAgentValidationAsync(
                    instance.WorkflowId,
                    instance.Id,
                    targetDefinitionStep,
                    userId,
                    request.ApAgent,
                    legacyTransactionId: null,
                    cancellationToken);
            }

            return new MoveToNextStepCommandResult(
                true,
                "AP agent review recorded; workflow not advanced (review is not Approve).",
                null,
                null,
                null,
                WorkflowCompleted: false);
        }

        if (isApAgentStep && routesByAction)
        {
            var apStepInstance = WorkflowStepTransitionHelper.FindStepInstance(instance, targetDefinitionStep.Id);
            if (apStepInstance == null)
                throw new InvalidOperationException(
                    $"No workflow step instance for AP agent activity '{request.ActivityId.Trim()}'.");

            if (apStepInstance.Status == StepInstanceStatus.Completed)
            {
                var nextAfterAp = orderedSteps
                    .Where(s => s.Order > targetDefinitionStep.Order)
                    .OrderBy(s => s.Order)
                    .FirstOrDefault();
                var nextInstance = nextAfterAp != null
                    ? WorkflowStepTransitionHelper.FindStepInstance(instance, nextAfterAp.Id)
                    : null;

                return new MoveToNextStepCommandResult(
                    true,
                    "AP agent step already completed.",
                    nextInstance?.Id,
                    nextAfterAp?.Name,
                    nextAfterAp?.Order,
                    instance.Status == WorkflowInstanceStatus.Completed,
                    instance.Id);
            }

            if (apStepInstance.Status is not (StepInstanceStatus.InProgress or StepInstanceStatus.WaitingForApproval))
                throw new InvalidOperationException(
                    $"AP agent step is not active (status: {apStepInstance.Status}).");
        }

        var mailboxForm = await BuildMailboxFormSnapshotAsync(request, cancellationToken);

        var legacySync = await _legacyTransactionSync.SyncTransactionByActivityIdAsync(
            instance.WorkflowId,
            instance.Id,
            instance.ReferenceNumber,
            targetDefinitionStep,
            orderedSteps,
            request.ActivityId.Trim(),
            userId,
            request.ActivityUserId,
            request.Review,
            mailboxForm,
            cancellationToken);

        WorkflowStep? nextDefinitionStep = null;
        var workflowCompleted = legacySync.WorkflowCompleted;

        if (legacySync.Status == LegacyTransactionSyncStatus.ReviewUpdated)
        {
            nextDefinitionStep = WorkflowStepActionsHelper.ResolveNextStepByReview(
                    targetDefinitionStep, request.Review, orderedSteps)
                ?? orderedSteps
                    .Where(s => s.Order > targetDefinitionStep.Order)
                    .OrderBy(s => s.Order)
                    .FirstOrDefault();

            WorkflowStepTransitionHelper.CompleteStepInstance(instance, targetDefinitionStep.Id, userId);
            if (nextDefinitionStep != null && !workflowCompleted)
                WorkflowStepTransitionHelper.StartStepInstance(instance, nextDefinitionStep.Id);
            else
            {
                instance.Complete(userId);
                workflowCompleted = true;
            }

            await _repository.UpdateInstanceAsync(instance, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (isApAgentStep && routesByAction && request.ApAgent != null)
            {
                await _apAgentMoveNext.AfterApAgentApproveAsync(
                    instance.WorkflowId,
                    instance.Id,
                    targetDefinitionStep,
                    instance.TenantId,
                    userId,
                    request.ApAgent,
                    legacySync.CurrentTransactionId,
                    cancellationToken);
            }
        }
        else if (workflowCompleted)
        {
            WorkflowStepTransitionHelper.CompleteStepInstance(instance, targetDefinitionStep.Id, userId);
            instance.Complete(userId);
            await _repository.UpdateInstanceAsync(instance, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var message = legacySync.Status switch
        {
            LegacyTransactionSyncStatus.StepInserted when workflowCompleted =>
                "END stage inserted; workflow completed.",
            LegacyTransactionSyncStatus.StepInserted => $"Step {targetDefinitionStep.Order} inserted in transaction table.",
            LegacyTransactionSyncStatus.StepAlreadyThere => "Step is already there.",
            LegacyTransactionSyncStatus.ReviewAlreadyUpdated => "Review is already updated.",
            LegacyTransactionSyncStatus.ReviewUpdated when workflowCompleted =>
                "Review updated; workflow completed.",
            LegacyTransactionSyncStatus.ReviewUpdated when legacySync.NextTransactionId.HasValue =>
                "Review updated and next step inserted.",
            LegacyTransactionSyncStatus.ReviewUpdated =>
                "Review updated.",
            _ => "OK"
        };

        if (!string.IsNullOrWhiteSpace(request.Comments)
            && !WorkflowCommentHelper.IsProceedActionSystemComment(request.Comments))
        {
            var stepInstanceId = WorkflowStepTransitionHelper.FindStepInstance(instance, targetDefinitionStep.Id)?.Id;
            await _dynamicTableRepository.AddCommentAsync(
                instance.WorkflowId,
                instance.Id,
                request.Comments.Trim(),
                userId,
                stepInstanceId,
                cancellationToken: cancellationToken);
        }

        var displayStep = nextDefinitionStep ?? targetDefinitionStep;
        var displayStepInstance = WorkflowStepTransitionHelper.FindStepInstance(instance, displayStep.Id);

        var isCompleted = workflowCompleted || instance.Status == WorkflowInstanceStatus.Completed;
        int? legacyNextTransactionId = isCompleted ? 0 : legacySync.NextTransactionId;
        Guid? legacyNextTransactionGuid = isCompleted ? null : legacySync.NextTransactionGuid;

        await PropagateMailboxFormDataAsync(request, instance, cancellationToken);

        return new MoveToNextStepCommandResult(
            true,
            message,
            displayStepInstance?.Id,
            displayStep.Name,
            displayStep.Order,
            isCompleted,
            legacySync.WorkflowInstanceId,
            legacySync.CurrentTransactionId,
            legacyNextTransactionId,
            legacyNextTransactionGuid);
    }

    internal static WorkflowStep? ResolveStepByActivityId(IReadOnlyList<WorkflowStep> orderedSteps, string activityId)
    {
        var id = activityId.Trim();
        return orderedSteps.FirstOrDefault(s =>
            (!string.IsNullOrWhiteSpace(s.ActivityId) &&
             string.Equals(s.ActivityId, id, StringComparison.OrdinalIgnoreCase))
            || string.Equals(s.Id.ToString("D"), id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Id.ToString("N"), id, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasUserFormData(MoveToNextStepCommand request) =>
        !string.IsNullOrWhiteSpace(request.SubmittedFormDataJson)
        || request.FormDataFields is { Count: > 0 }
        || !string.IsNullOrWhiteSpace(request.FormLineItemsJson);

    private async Task<MailboxFormSnapshot?> BuildMailboxFormSnapshotAsync(
        MoveToNextStepCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FormId) || request.FormEntryId is not > 0)
            return null;

        string? formDataJson;
        if (HasUserFormData(request))
        {
            formDataJson = MoveToNextStepFormDataComposer.ForMailbox(request.SubmittedFormDataJson)
                ?? MoveToNextStepFormDataComposer.FromParsedFields(
                    request.FormDataFields,
                    request.FormLineItemsJson);
        }
        else
        {
            formDataJson = await _ezfbFormDataLoader.LoadFormDataJsonAsync(
                request.FormId,
                request.FormEntryId.Value,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(formDataJson))
            return null;

        return new MailboxFormSnapshot(request.FormId, request.FormEntryId, formDataJson);
    }

    private async Task PropagateMailboxFormDataAsync(
        MoveToNextStepCommand request,
        WorkflowInstance instance,
        CancellationToken cancellationToken)
    {
        var snapshot = await BuildMailboxFormSnapshotAsync(request, cancellationToken);
        if (snapshot == null)
            return;

        await _mailboxSync.PropagateInstanceFormDataAsync(
            instance.WorkflowId,
            instance.Id,
            snapshot,
            cancellationToken);
    }

}
