using MediatR;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Commands.PublishWorkflow;

public sealed class PublishWorkflowCommandHandler : IRequestHandler<PublishWorkflowCommand, PublishWorkflowCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowTableCreator _tableCreator;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PublishWorkflowCommandHandler> _logger;

    public PublishWorkflowCommandHandler(IWorkflowRepository repository, IUnitOfWork unitOfWork, IWorkflowTableCreator tableCreator, ITenantContext tenantContext, ILogger<PublishWorkflowCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _tableCreator = tableCreator;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PublishWorkflowCommandResult> Handle(PublishWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await _repository.GetByIdWithStepsAsync(request.WorkflowId, cancellationToken);
        if (workflow == null || workflow.IsDeleted)
            return new PublishWorkflowCommandResult(false);

        workflow.Publish();
        _repository.Update(workflow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Create dynamic tables for this workflow (Comments_X, Attachments_X, etc.)
        var connectionString = _tenantContext.ConnectionString;
        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                await _tableCreator.CreateWorkflowTablesAsync(workflow.Id, connectionString, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log but don't fail publish - workflow is already saved; dynamic tables can be created later
                _logger.LogWarning(ex, "WorkflowTableCreator failed for workflow {WorkflowId}", workflow.Id);
            }
        }

        return new PublishWorkflowCommandResult(true);
    }
}
