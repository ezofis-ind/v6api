namespace SaaSApp.Workflow.Application.Contracts;

public interface IApDashboardQueryService
{
  Task<ApDashboardResult> GetDashboardAsync(
    Guid tenantId,
    ApDashboardRequest request,
    CancellationToken cancellationToken = default);
}
