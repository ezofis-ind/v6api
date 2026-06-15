namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>Current authenticated user context.</summary>
public interface ICurrentUserProvider
{
    /// <summary>Current user ID from JWT.</summary>
    Guid? GetUserId();
}
