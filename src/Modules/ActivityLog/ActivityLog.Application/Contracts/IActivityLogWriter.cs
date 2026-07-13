namespace SaaSApp.ActivityLog.Application.Contracts;

public interface IActivityLogWriter
{
    void Enqueue(ActivityLogEntry entry, string connectionString);
}
