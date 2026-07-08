namespace SaaSApp.Catalog;

/// <summary>
/// Registers a user (email) as a member of a tenant with a role. Used for "my organizations" at login.
/// </summary>
public interface IUserTenantRegistry
{
    Task AddOrUpdateAsync(
        string email,
        Guid tenantId,
        string role,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<UserPreQuestionsResponse?> GetPreQuestionsAsync(
        Guid userId,
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default);

    Task<bool> UpdatePreQuestionsAsync(
        Guid userId,
        Guid tenantId,
        string email,
        IReadOnlyList<PreQuestionAnswerDto> questions,
        CancellationToken cancellationToken = default);
}
