using MediatR;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Entities;

namespace SaaSApp.Workflow.Application.Workflows.Commands.SetWorkflowSla;

public sealed class SetWorkflowSlaCommandHandler : IRequestHandler<SetWorkflowSlaCommand, SetWorkflowSlaCommandResult>
{
    private readonly IWorkflowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public SetWorkflowSlaCommandHandler(IWorkflowRepository repository, IUnitOfWork unitOfWork, ITenantContext tenantContext)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<SetWorkflowSlaCommandResult> Handle(SetWorkflowSlaCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

        var workflow = await _repository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow == null || workflow.IsDeleted || workflow.TenantId != tenantId)
            throw new InvalidOperationException("Workflow not found.");

        WorkflowSla sla;
        if (workflow.Sla == null)
        {
            sla = WorkflowSla.Create(tenantId, workflow.Id, request.Priority, request.ResponseTimeMinutes, request.ResolutionTimeMinutes, request.EscalationTimeMinutes, request.EscalateToUserId, request.EscalateToRole, request.SendNotificationOnBreach, request.NotificationEmails);
            workflow.SetSla(sla);
        }
        else
        {
            sla = workflow.Sla;
            sla.Update(request.Priority, request.ResponseTimeMinutes, request.ResolutionTimeMinutes, request.EscalationTimeMinutes, request.EscalateToUserId, request.EscalateToRole, request.SendNotificationOnBreach, request.NotificationEmails);
        }

        _repository.Update(workflow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetWorkflowSlaCommandResult(sla.Id);
    }
}
