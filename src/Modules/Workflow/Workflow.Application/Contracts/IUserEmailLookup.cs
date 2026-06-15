namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Resolves user GUIDs to email addresses from users.Users on the tenant database.</summary>
public interface IUserEmailLookup
{
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);
}
