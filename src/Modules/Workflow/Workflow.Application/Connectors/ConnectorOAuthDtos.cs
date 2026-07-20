namespace SaaSApp.Workflow.Application.Connectors;

public sealed record ConnectorProviderPublicDto(
    string ProviderCode,
    string DisplayName,
    bool IsConfigured,
    bool SupportsFiles,
    bool SupportsGmail,
    bool SupportsQuickBooks = false);

public sealed record ConnectorOAuthAuthorizeRequest(
    string ProviderCode,
    Guid? ConnectorId = null,
    string? Name = null,
    string? ConfigJson = null,
    string? SuccessRedirectUrl = null);

public sealed record ConnectorOAuthAuthorizeResponse(
    Guid ConnectorId,
    string AuthorizationUrl,
    string State);

public sealed record ConnectorOAuthStatusDto(
    Guid ConnectorId,
    string? ProviderCode,
    string OAuthStatus,
    string? ExternalAccountEmail,
    DateTime? TokenExpiresAtUtc,
    bool IsConnected);

public sealed record ConnectorFileEntryDto(
    string Path,
    string Name,
    bool IsFolder,
    long? SizeBytes,
    DateTime? ModifiedAtUtc);

public sealed record ConnectorFileListResponse(IReadOnlyList<ConnectorFileEntryDto> Items);

public sealed record ConnectorGmailMessageDto(
    string Id,
    string? ThreadId,
    string? Subject,
    string? From,
    string? Snippet,
    DateTime? ReceivedAtUtc,
    IReadOnlyList<ConnectorGmailAttachmentDto> Attachments,
    bool IsUnread = false,
    string? BodyText = null,
    string? BodyHtml = null);

public sealed record ConnectorGmailAttachmentDto(
    string Id,
    string? FileName,
    string? MimeType,
    long? SizeBytes);

public sealed record ConnectorGmailMessageListResponse(IReadOnlyList<ConnectorGmailMessageDto> Items);

public sealed record ConnectorMailSummaryDto(int TotalCount, int UnreadCount);

public sealed record ConnectorQuickBooksMasterDto(
    string Id,
    string Type,
    string? DisplayName,
    string? Email,
    bool Active,
    string? RawJson);

public sealed record ConnectorQuickBooksMasterListResponse(string Type, IReadOnlyList<ConnectorQuickBooksMasterDto> Items);

public sealed record ConnectorQuickBooksDocumentDto(
    string Id,
    string Type,
    string? DocNumber,
    string? TxnDate,
    decimal? TotalAmount,
    string? CustomerVendorName,
    string? Status,
    string? RawJson);

public sealed record ConnectorQuickBooksDocumentListResponse(string Type, IReadOnlyList<ConnectorQuickBooksDocumentDto> Items);

