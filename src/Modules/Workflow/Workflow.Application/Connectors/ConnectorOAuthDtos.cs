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

/// <summary>AP Agent payload: look up a QuickBooks Purchase Order by DocNumber (PO Number).</summary>
public sealed record ConnectorQuickBooksPoLookupRequest(string PoNumber);

public sealed record ConnectorQuickBooksPoLineDto(
    string? LineId,
    int? LineNum,
    string? DetailType,
    string? Description,
    decimal? Amount,
    string? ItemId,
    string? ItemName,
    decimal? Quantity,
    decimal? UnitPrice,
    string? AccountId,
    string? AccountName);

public sealed record ConnectorQuickBooksPurchaseOrderDto(
    string Id,
    string? DocNumber,
    string? TxnDate,
    string? DueDate,
    string? VendorId,
    string? VendorName,
    decimal? TotalAmount,
    string? Currency,
    string? PoStatus,
    string? EmailStatus,
    string? Memo,
    IReadOnlyList<ConnectorQuickBooksPoLineDto> Lines,
    object? Raw);

public sealed record ConnectorQuickBooksPoLookupResponse(
    bool Found,
    string PoNumber,
    ConnectorQuickBooksPurchaseOrderDto? PurchaseOrder);

