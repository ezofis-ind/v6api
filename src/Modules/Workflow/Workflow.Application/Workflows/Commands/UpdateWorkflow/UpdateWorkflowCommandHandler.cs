using System.Text.Json;
using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows;
using static SaaSApp.Workflow.Application.Workflows.WorkflowJsonSerializerOptions;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;
using SaaSApp.Workflow.Domain.Enums;
using WorkflowEntity = SaaSApp.Workflow.Domain.Entities.Workflow;

namespace SaaSApp.Workflow.Application.Workflows.Commands.UpdateWorkflow;

public sealed class UpdateWorkflowCommandHandler : IRequestHandler<UpdateWorkflowCommand, UpdateWorkflowCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITenantContext _tenantContext;
    private readonly IWorkflowTableCreator _tableCreator;
    private readonly IWorkflowJsonStorageService _jsonStorage;
    private readonly IWorkflowSecurityService _securityService;
    private readonly IWorkflowInitiationService _initiationService;
    private readonly IWorkflowSlaService _slaService;
    private readonly IWorkflowMlService _mlService;
    private readonly IWorkflowStepSyncService _stepSyncService;

    public UpdateWorkflowCommandHandler(
        IWorkflowRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ITenantContext tenantContext,
        IWorkflowTableCreator tableCreator,
        IWorkflowJsonStorageService jsonStorage,
        IWorkflowSecurityService securityService,
        IWorkflowInitiationService initiationService,
        IWorkflowSlaService slaService,
        IWorkflowMlService mlService,
        IWorkflowStepSyncService stepSyncService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _tenantContext = tenantContext;
        _tableCreator = tableCreator;
        _jsonStorage = jsonStorage;
        _securityService = securityService;
        _initiationService = initiationService;
        _slaService = slaService;
        _mlService = mlService;
        _stepSyncService = stepSyncService;
    }

    public async Task<UpdateWorkflowCommandResult> Handle(UpdateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");
        var workflow = await _repository.GetByIdWithStepsAsync(request.WorkflowId, cancellationToken);
        if (workflow == null || workflow.IsDeleted)
            return new UpdateWorkflowCommandResult(false);

        if (request.WorkflowJson != null)
            return await HandleFullJsonUpdateAsync(workflow, request, userId, cancellationToken);

        workflow.Update(request.Name, request.Description, request.TriggerType, request.TriggerConfig, userId);
        _repository.Update(workflow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new UpdateWorkflowCommandResult(true);
    }

    private async Task<UpdateWorkflowCommandResult> HandleFullJsonUpdateAsync(
        WorkflowEntity workflow,
        UpdateWorkflowCommand request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var json = request.WorkflowJson!;

        var workflowName = json.Settings?.General?.Name ?? request.Name ?? workflow.Name;
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new InvalidOperationException("Workflow name is required.");

        workflowName = workflowName.Trim();
        var description = json.Settings?.General?.Description ?? request.Description ?? workflow.Description;

        var publishOption = json.Settings?.Publish?.PublishOption;
        bool isPublished;
        if (publishOption != null)
            isPublished = publishOption.Equals("PUBLISHED", StringComparison.OrdinalIgnoreCase);
        else if (request.PublishImmediately)
            isPublished = true;
        else
            isPublished = workflow.Status == WorkflowStatus.Active;

        var other = await _repository.GetByNameAsync(workflowName, tenantId, cancellationToken);
        if (other != null && other.Id != workflow.Id)
            return new UpdateWorkflowCommandResult(true, NameConflict: true);

        workflow.Update(workflowName, description, request.TriggerType, request.TriggerConfig, userId);
        var (repositoryId, formId) = WorkflowInitiateLinksHelper.FromInitiateUsing(json.Settings?.General?.InitiateUsing);
        workflow.SetInitiateLinks(repositoryId, formId, userId);

        var workflowJsonString = !string.IsNullOrWhiteSpace(request.WorkflowJsonRaw)
            ? request.WorkflowJsonRaw
            : JsonSerializer.Serialize(json, Storage);
        await _jsonStorage.SaveWorkflowJsonAsync(workflow.Id, workflowJsonString, cancellationToken);

        _repository.Update(workflow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // For designer PUT updates, always ensure workflow dynamic tables exist.
        // The previous condition required PUBLISHED; that prevented table creation when staying in DRAFT.
        var connectionString = _tenantContext.ConnectionString;
        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                await _tableCreator.CreateWorkflowTablesAsync(workflow.Id, connectionString, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create workflow tables on update: {ex.Message}");
            }
        }

        await _securityService.SetWorkflowSecurityAsync(
            workflow.Id,
            json.Settings?.General?.Coordinator,
            json.Settings?.General?.Superuser,
            json.Blocks,
            cancellationToken);

        // Always sync WorkflowSteps from PUT body; if blocks missing, load from blob just saved.
        await _stepSyncService.SyncStepsFromWorkflowJsonAsync(workflow.Id, json, cancellationToken);

        if (json.InitiateUserDomain != null && json.InitiateUserDomain.Length > 0)
        {
            await _securityService.SetWorkflowUsersByDomainAsync(
                workflow.Id,
                json.InitiateUserDomain,
                cancellationToken);
        }

        if (isPublished)
        {
            await _slaService.CreateSlaRulesAsync(
                workflow.Id,
                json.Settings?.General?.SlaRules,
                json.Blocks,
                cancellationToken);
        }

        if (json.Blocks != null && json.Settings?.General?.InitiateUsing != null)
        {
            await _initiationService.SetupAutoInitiationAsync(
                workflow.Id,
                json.Blocks,
                json.Settings.General.InitiateUsing,
                json.Rules,
                cancellationToken);
        }

        if (json.Blocks != null)
        {
            await _mlService.CreateMlPredictionsAsync(
                workflow.Id,
                json.Blocks,
                cancellationToken);
        }

        if (isPublished && workflow.Status != WorkflowStatus.Active)
        {
            workflow.Publish();
            _repository.Update(workflow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (!isPublished && workflow.Status == WorkflowStatus.Active)
        {
            workflow.RevertToDraft(userId);
            _repository.Update(workflow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new UpdateWorkflowCommandResult(true);
    }
}
