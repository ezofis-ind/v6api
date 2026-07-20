using SaaSApp.Workflow.Application.Connectors;

namespace SaaSApp.Workflow.Application.Contracts;

public interface IEmailIngestService
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailIngestMailboxDto>> ListMailboxesAsync(CancellationToken cancellationToken = default);

    Task<EmailIngestMailboxDto?> GetMailboxAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmailIngestMailboxDto?> GetMailboxByWorkflowIdAsync(Guid workflowId, CancellationToken cancellationToken = default);

    Task<EmailIngestMailboxDto> CreateMailboxAsync(
        EmailIngestMailboxUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<EmailIngestMailboxDto?> UpdateMailboxAsync(
        Guid id,
        EmailIngestMailboxUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteMailboxAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmailIngestPollResultDto> PollMailboxAsync(Guid mailboxId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailIngestPollResultDto>> PollDueMailboxesAsync(CancellationToken cancellationToken = default);
}
