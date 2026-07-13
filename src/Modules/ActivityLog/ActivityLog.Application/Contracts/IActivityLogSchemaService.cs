namespace SaaSApp.ActivityLog.Application.Contracts;

public interface IActivityLogSchemaService
{
    Task ApplyBaseSchemaAsync(string connectionString, CancellationToken cancellationToken = default);
}
