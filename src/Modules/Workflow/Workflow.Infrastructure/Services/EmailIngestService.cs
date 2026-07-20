using System.Text.Json;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Connectors;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Application.Workflows.Commands.StartWorkflow;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed class EmailIngestService : IEmailIngestService
{
    private const string DefaultExtensions = ".pdf,.png,.jpg,.jpeg,.tif,.tiff";
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ITenantContext _tenantContext;
    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IConnectorService _connectorService;
    private readonly IConnectorOAuthService _oauthService;
    private readonly IMediator _mediator;
    private readonly ILogger<EmailIngestService> _logger;

    public EmailIngestService(
        ITenantContext tenantContext,
        ITenantConnectionProvider connectionProvider,
        ICurrentUserProvider currentUserProvider,
        IConnectorService connectorService,
        IConnectorOAuthService oauthService,
        IMediator mediator,
        ILogger<EmailIngestService> logger)
    {
        _tenantContext = tenantContext;
        _connectionProvider = connectionProvider;
        _currentUserProvider = currentUserProvider;
        _connectorService = connectorService;
        _oauthService = oauthService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            IF OBJECT_ID(N'dbo.EmailIngestMailbox', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmailIngestMailbox (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmailIngestMailbox PRIMARY KEY,
                    ConnectorId UNIQUEIDENTIFIER NOT NULL,
                    WorkflowId UNIQUEIDENTIFIER NOT NULL,
                    IsEnabled BIT NOT NULL CONSTRAINT DF_EmailIngestMailbox_IsEnabled DEFAULT (1),
                    PollIntervalMinutes INT NOT NULL CONSTRAINT DF_EmailIngestMailbox_PollInterval DEFAULT (5),
                    QueryFilter NVARCHAR(512) NULL,
                    MasterSource NVARCHAR(32) NOT NULL CONSTRAINT DF_EmailIngestMailbox_MasterSource DEFAULT (N'InternalForm'),
                    MasterFormId NVARCHAR(128) NULL,
                    MasterConnectorId UNIQUEIDENTIFIER NULL,
                    AttachmentExtensions NVARCHAR(256) NOT NULL CONSTRAINT DF_EmailIngestMailbox_Ext DEFAULT (N'.pdf,.png,.jpg,.jpeg,.tif,.tiff'),
                    LastPolledAtUtc DATETIME2(3) NULL,
                    LastError NVARCHAR(2000) NULL,
                    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EmailIngestMailbox_Created DEFAULT (SYSUTCDATETIME()),
                    ModifiedAtUtc DATETIME2(3) NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    ModifiedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_EmailIngestMailbox_IsDeleted DEFAULT (0)
                );
            END

            IF OBJECT_ID(N'dbo.EmailIngestProcessed', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmailIngestProcessed (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmailIngestProcessed PRIMARY KEY,
                    MailboxId UNIQUEIDENTIFIER NOT NULL,
                    ProviderMessageId NVARCHAR(256) NOT NULL,
                    AttachmentId NVARCHAR(256) NOT NULL,
                    WorkflowInstanceId UNIQUEIDENTIFIER NULL,
                    ProcessedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EmailIngestProcessed_At DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT UQ_EmailIngestProcessed UNIQUE (MailboxId, ProviderMessageId, AttachmentId)
                );
            END
            """;
        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailIngestMailboxDto>> ListMailboxesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT m.Id, m.ConnectorId, m.WorkflowId, m.IsEnabled, m.PollIntervalMinutes, m.QueryFilter,
                   m.MasterSource, m.MasterFormId, m.MasterConnectorId, m.AttachmentExtensions,
                   m.LastPolledAtUtc, m.LastError, m.CreatedAtUtc, m.ModifiedAtUtc,
                   c.Name, c.ProviderCode, c.ExternalAccountEmail
            FROM dbo.EmailIngestMailbox m
            LEFT JOIN dbo.connector c ON c.Id = m.ConnectorId AND c.IsDeleted = 0
            WHERE m.IsDeleted = 0
            ORDER BY m.CreatedAtUtc DESC;
            """;
        var list = new List<EmailIngestMailboxDto>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(MapMailbox(reader));
        return list;
    }

    public async Task<EmailIngestMailboxDto?> GetMailboxAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT m.Id, m.ConnectorId, m.WorkflowId, m.IsEnabled, m.PollIntervalMinutes, m.QueryFilter,
                   m.MasterSource, m.MasterFormId, m.MasterConnectorId, m.AttachmentExtensions,
                   m.LastPolledAtUtc, m.LastError, m.CreatedAtUtc, m.ModifiedAtUtc,
                   c.Name, c.ProviderCode, c.ExternalAccountEmail
            FROM dbo.EmailIngestMailbox m
            LEFT JOIN dbo.connector c ON c.Id = m.ConnectorId AND c.IsDeleted = 0
            WHERE m.Id = @Id AND m.IsDeleted = 0;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return MapMailbox(reader);
    }

    public async Task<EmailIngestMailboxDto?> GetMailboxByWorkflowIdAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP 1 m.Id, m.ConnectorId, m.WorkflowId, m.IsEnabled, m.PollIntervalMinutes, m.QueryFilter,
                   m.MasterSource, m.MasterFormId, m.MasterConnectorId, m.AttachmentExtensions,
                   m.LastPolledAtUtc, m.LastError, m.CreatedAtUtc, m.ModifiedAtUtc,
                   c.Name, c.ProviderCode, c.ExternalAccountEmail
            FROM dbo.EmailIngestMailbox m
            LEFT JOIN dbo.connector c ON c.Id = m.ConnectorId AND c.IsDeleted = 0
            WHERE m.WorkflowId = @WorkflowId AND m.IsDeleted = 0
            ORDER BY m.CreatedAtUtc DESC;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@WorkflowId", workflowId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return MapMailbox(reader);
    }

    public async Task<EmailIngestMailboxDto> CreateMailboxAsync(
        EmailIngestMailboxUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await ValidateRequestAsync(request, cancellationToken);

        var id = Guid.NewGuid();
        var userId = _currentUserProvider.GetUserId();
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.EmailIngestMailbox
                (Id, ConnectorId, WorkflowId, IsEnabled, PollIntervalMinutes, QueryFilter,
                 MasterSource, MasterFormId, MasterConnectorId, AttachmentExtensions,
                 CreatedAtUtc, CreatedBy, IsDeleted)
            VALUES
                (@Id, @ConnectorId, @WorkflowId, @IsEnabled, @PollIntervalMinutes, @QueryFilter,
                 @MasterSource, @MasterFormId, @MasterConnectorId, @AttachmentExtensions,
                 SYSUTCDATETIME(), @CreatedBy, 0);
            """;
        await using var cmd = new SqlCommand(sql, connection);
        BindUpsert(cmd, id, request, userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return (await GetMailboxAsync(id, cancellationToken))!;
    }

    public async Task<EmailIngestMailboxDto?> UpdateMailboxAsync(
        Guid id, EmailIngestMailboxUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        if (await GetMailboxAsync(id, cancellationToken) == null)
            return null;

        await ValidateRequestAsync(request, cancellationToken);
        var userId = _currentUserProvider.GetUserId();
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.EmailIngestMailbox
            SET ConnectorId = @ConnectorId,
                WorkflowId = @WorkflowId,
                IsEnabled = @IsEnabled,
                PollIntervalMinutes = @PollIntervalMinutes,
                QueryFilter = @QueryFilter,
                MasterSource = @MasterSource,
                MasterFormId = @MasterFormId,
                MasterConnectorId = @MasterConnectorId,
                AttachmentExtensions = @AttachmentExtensions,
                ModifiedAtUtc = SYSUTCDATETIME(),
                ModifiedBy = @CreatedBy
            WHERE Id = @Id AND IsDeleted = 0;
            """;
        await using var cmd = new SqlCommand(sql, connection);
        BindUpsert(cmd, id, request, userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return await GetMailboxAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteMailboxAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            """
            UPDATE dbo.EmailIngestMailbox
            SET IsDeleted = 1, ModifiedAtUtc = SYSUTCDATETIME()
            WHERE Id = @Id AND IsDeleted = 0;
            """, connection);
        cmd.Parameters.AddWithValue("@Id", id);
        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<EmailIngestPollResultDto>> PollDueMailboxesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var mailboxes = await ListMailboxesAsync(cancellationToken);
        var due = mailboxes.Where(m =>
            m.IsEnabled &&
            (m.LastPolledAtUtc == null ||
             m.LastPolledAtUtc.Value.AddMinutes(Math.Max(1, m.PollIntervalMinutes)) <= DateTime.UtcNow));

        var results = new List<EmailIngestPollResultDto>();
        foreach (var mailbox in due)
            results.Add(await PollMailboxAsync(mailbox.Id, cancellationToken));
        return results;
    }

    public async Task<EmailIngestPollResultDto> PollMailboxAsync(Guid mailboxId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var mailbox = await GetMailboxAsync(mailboxId, cancellationToken)
            ?? throw new InvalidOperationException("Mailbox not found.");

        var scanned = 0;
        var started = 0;
        var skipped = 0;
        string? error = null;

        try
        {
            var messages = await _oauthService.ListGmailMessagesAsync(
                mailbox.ConnectorId,
                maxResults: 25,
                query: mailbox.QueryFilter,
                unreadOnly: true,
                cancellationToken);

            var extensions = ParseExtensions(mailbox.AttachmentExtensions);
            foreach (var message in messages.Items)
            {
                scanned++;
                var matching = message.Attachments
                    .Where(a => MatchesExtension(a.FileName, extensions))
                    .ToList();

                if (matching.Count == 0)
                    continue;

                var anyStarted = false;
                foreach (var att in matching)
                {
                    if (await IsProcessedAsync(mailboxId, message.Id, att.Id, cancellationToken))
                    {
                        skipped++;
                        continue;
                    }

                    var (stream, contentType, fileName) = await _oauthService.DownloadGmailAttachmentAsync(
                        mailbox.ConnectorId, message.Id, att.Id, cancellationToken);
                    await using (stream)
                    {
                        await using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms, cancellationToken);
                        var bytes = ms.ToArray();
                        if (bytes.Length == 0)
                            continue;

                        var contextJson = JsonSerializer.Serialize(new Dictionary<string, object?>
                        {
                            ["emailIngest"] = true,
                            ["mailboxId"] = mailbox.Id,
                            ["connectorId"] = mailbox.ConnectorId,
                            ["messageId"] = message.Id,
                            ["from"] = message.From,
                            ["subject"] = message.Subject,
                            ["receivedAtUtc"] = message.ReceivedAtUtc,
                            ["attachmentId"] = att.Id,
                            ["attachmentFileName"] = fileName ?? att.FileName,
                            ["masterSource"] = mailbox.MasterSource,
                            ["masterFormId"] = mailbox.MasterFormId,
                            ["masterConnectorId"] = mailbox.MasterConnectorId
                        });

                        var startResult = await _mediator.Send(new StartWorkflowCommand(
                            mailbox.WorkflowId,
                            Context: contextJson,
                            Attachment: new StartWorkflowAttachmentPayload(
                                bytes,
                                fileName ?? att.FileName ?? "invoice.bin",
                                contentType ?? att.MimeType),
                            TriggerApAgentPythonJob: true), cancellationToken);

                        await MarkProcessedAsync(mailboxId, message.Id, att.Id, startResult.InstanceId, cancellationToken);
                        started++;
                        anyStarted = true;
                    }
                }

                if (anyStarted)
                {
                    try
                    {
                        await _oauthService.MarkMailMessageReadAsync(mailbox.ConnectorId, message.Id, cancellationToken);
                    }
                    catch (Exception markEx)
                    {
                        _logger.LogWarning(markEx,
                            "Email ingest started workflows but mark-as-read failed for message {MessageId}",
                            message.Id);
                    }
                }
            }

            await UpdatePollStatusAsync(mailboxId, null, cancellationToken);
        }
        catch (Exception ex)
        {
            error = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
            await UpdatePollStatusAsync(mailboxId, error, cancellationToken);
            _logger.LogError(ex, "Email ingest poll failed for mailbox {MailboxId}", mailboxId);
        }

        return new EmailIngestPollResultDto(mailboxId, scanned, started, skipped, error);
    }

    private async Task ValidateRequestAsync(EmailIngestMailboxUpsertRequest request, CancellationToken cancellationToken)
    {
        if (request.ConnectorId == Guid.Empty)
            throw new InvalidOperationException("connectorId is required.");
        if (request.WorkflowId == Guid.Empty)
            throw new InvalidOperationException("workflowId is required.");

        var connector = await _connectorService.GetByIdAsync(request.ConnectorId, cancellationToken)
            ?? throw new InvalidOperationException("Connector not found.");
        var code = (connector.ProviderCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code is not ("GMAIL" or "OUTLOOK"))
            throw new InvalidOperationException("Mailbox connector must be GMAIL or OUTLOOK.");

        var source = (request.MasterSource ?? EmailIngestMasterSources.InternalForm).Trim();
        if (string.Equals(source, EmailIngestMasterSources.InternalForm, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.MasterFormId))
                throw new InvalidOperationException("masterFormId is required when masterSource is InternalForm.");
        }
        else if (string.Equals(source, EmailIngestMasterSources.QuickBooks, StringComparison.OrdinalIgnoreCase))
        {
            if (request.MasterConnectorId is not { } masterConnectorId || masterConnectorId == Guid.Empty)
                throw new InvalidOperationException("masterConnectorId is required when masterSource is QuickBooks.");
            var masterConn = await _connectorService.GetByIdAsync(masterConnectorId, cancellationToken)
                ?? throw new InvalidOperationException("Master QuickBooks connector not found.");
            if (!string.Equals(masterConn.ProviderCode, "QUICKBOOKS", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("masterConnectorId must be a QUICKBOOKS connector.");
        }
        else
            throw new InvalidOperationException("masterSource must be InternalForm or QuickBooks.");

        if (request.PollIntervalMinutes < 1 || request.PollIntervalMinutes > 1440)
            throw new InvalidOperationException("pollIntervalMinutes must be between 1 and 1440.");
    }

    private static void BindUpsert(SqlCommand cmd, Guid id, EmailIngestMailboxUpsertRequest request, Guid? userId)
    {
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@ConnectorId", request.ConnectorId);
        cmd.Parameters.AddWithValue("@WorkflowId", request.WorkflowId);
        cmd.Parameters.AddWithValue("@IsEnabled", request.IsEnabled);
        cmd.Parameters.AddWithValue("@PollIntervalMinutes", request.PollIntervalMinutes);
        cmd.Parameters.AddWithValue("@QueryFilter", (object?)request.QueryFilter ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MasterSource",
            string.IsNullOrWhiteSpace(request.MasterSource)
                ? EmailIngestMasterSources.InternalForm
                : request.MasterSource.Trim());
        cmd.Parameters.AddWithValue("@MasterFormId", (object?)request.MasterFormId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MasterConnectorId", (object?)request.MasterConnectorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AttachmentExtensions",
            string.IsNullOrWhiteSpace(request.AttachmentExtensions)
                ? DefaultExtensions
                : request.AttachmentExtensions.Trim());
        cmd.Parameters.AddWithValue("@CreatedBy", (object?)userId ?? SystemUserId);
    }

    private async Task<bool> IsProcessedAsync(
        Guid mailboxId, string messageId, string attachmentId, CancellationToken cancellationToken)
    {
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            """
            SELECT TOP 1 1 FROM dbo.EmailIngestProcessed
            WHERE MailboxId = @MailboxId AND ProviderMessageId = @MessageId AND AttachmentId = @AttachmentId;
            """, connection);
        cmd.Parameters.AddWithValue("@MailboxId", mailboxId);
        cmd.Parameters.AddWithValue("@MessageId", messageId);
        cmd.Parameters.AddWithValue("@AttachmentId", attachmentId);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null && result != DBNull.Value;
    }

    private async Task MarkProcessedAsync(
        Guid mailboxId, string messageId, string attachmentId, Guid? instanceId, CancellationToken cancellationToken)
    {
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            """
            INSERT INTO dbo.EmailIngestProcessed (Id, MailboxId, ProviderMessageId, AttachmentId, WorkflowInstanceId, ProcessedAtUtc)
            VALUES (@Id, @MailboxId, @MessageId, @AttachmentId, @InstanceId, SYSUTCDATETIME());
            """, connection);
        cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@MailboxId", mailboxId);
        cmd.Parameters.AddWithValue("@MessageId", messageId);
        cmd.Parameters.AddWithValue("@AttachmentId", attachmentId);
        cmd.Parameters.AddWithValue("@InstanceId", (object?)instanceId ?? DBNull.Value);
        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            // unique violation — already processed
        }
    }

    private async Task UpdatePollStatusAsync(Guid mailboxId, string? error, CancellationToken cancellationToken)
    {
        var cs = RequireConnectionString();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            """
            UPDATE dbo.EmailIngestMailbox
            SET LastPolledAtUtc = SYSUTCDATETIME(),
                LastError = @Error,
                ModifiedAtUtc = SYSUTCDATETIME()
            WHERE Id = @Id AND IsDeleted = 0;
            """, connection);
        cmd.Parameters.AddWithValue("@Id", mailboxId);
        cmd.Parameters.AddWithValue("@Error", (object?)error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static HashSet<string> ParseExtensions(string? raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (raw ?? DefaultExtensions).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var ext = part.StartsWith('.') ? part : "." + part;
            set.Add(ext);
        }
        return set;
    }

    private static bool MatchesExtension(string? fileName, HashSet<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && extensions.Contains(ext);
    }

    private string RequireConnectionString() =>
        _connectionProvider.ConnectionString
        ?? _tenantContext.ConnectionString
        ?? throw new InvalidOperationException("Tenant connection string not resolved.");

    private static EmailIngestMailboxDto MapMailbox(SqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetBoolean(3),
            reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetDateTime(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.GetDateTime(12),
            reader.IsDBNull(13) ? null : reader.GetDateTime(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetString(16));
}
