using SaaSApp.Workflow.Application.Connectors;

namespace SaaSApp.Workflow.Application.Contracts;

public interface IConnectorOAuthService
{
    Task<IReadOnlyList<ConnectorProviderPublicDto>> ListProvidersAsync(CancellationToken cancellationToken = default);

    Task<ConnectorOAuthAuthorizeResponse> BeginAuthorizeAsync(
        ConnectorOAuthAuthorizeRequest request,
        CancellationToken cancellationToken = default);

    Task<string> CompleteCallbackAsync(
        string? code,
        string? state,
        string? error,
        string? realmId = null,
        CancellationToken cancellationToken = default);

    Task<ConnectorOAuthStatusDto?> RefreshAsync(Guid connectorId, CancellationToken cancellationToken = default);

    Task<ConnectorOAuthStatusDto?> DisconnectAsync(Guid connectorId, CancellationToken cancellationToken = default);

    Task<ConnectorOAuthStatusDto?> GetStatusAsync(Guid connectorId, CancellationToken cancellationToken = default);

    Task EnsureValidAccessTokenAsync(Guid connectorId, CancellationToken cancellationToken = default);

    Task<ConnectorFileListResponse> ListFilesAsync(Guid connectorId, string? path, CancellationToken cancellationToken = default);

    Task UploadFileAsync(
        Guid connectorId,
        string path,
        string fileName,
        Stream content,
        string? contentType,
        CancellationToken cancellationToken = default);

    Task<(Stream Content, string ContentType, string FileName)> DownloadFileAsync(
        Guid connectorId,
        string path,
        CancellationToken cancellationToken = default);

    Task<ConnectorMailSummaryDto> GetMailSummaryAsync(
        Guid connectorId,
        CancellationToken cancellationToken = default);

    Task<ConnectorGmailMessageListResponse> ListGmailMessagesAsync(
        Guid connectorId,
        int maxResults,
        string? query,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<ConnectorGmailMessageDto> GetMailMessageAsync(
        Guid connectorId,
        string messageId,
        CancellationToken cancellationToken = default);

    Task<ConnectorGmailMessageDto?> GetTopMailMessageAsync(
        Guid connectorId,
        bool unreadOnly = true,
        string? query = null,
        CancellationToken cancellationToken = default);

    Task MarkMailMessageReadAsync(
        Guid connectorId,
        string messageId,
        CancellationToken cancellationToken = default);

    Task<(Stream Content, string ContentType, string FileName)> DownloadGmailAttachmentAsync(
        Guid connectorId,
        string messageId,
        string attachmentId,
        CancellationToken cancellationToken = default);

    Task<ConnectorQuickBooksMasterListResponse> ListQuickBooksMastersAsync(
        Guid connectorId,
        string masterType,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<ConnectorQuickBooksDocumentListResponse> ListQuickBooksDocumentsAsync(
        Guid connectorId,
        string documentType,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<(Stream Content, string ContentType, string FileName)> DownloadQuickBooksDocumentPdfAsync(
        Guid connectorId,
        string documentType,
        string documentId,
        CancellationToken cancellationToken = default);
}
