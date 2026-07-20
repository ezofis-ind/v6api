using Microsoft.Data.SqlClient;
using SaaSApp.MultiTenancy;

namespace SaaSApp.Api.Services;

public sealed class SupportTicketInsertRequest
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid? UserId { get; init; }
    public string? CallerEmail { get; init; }
    public string? SupportCategory { get; init; }
    public string? Priorty { get; init; }
    public string? PreferredContact { get; init; }
    public string? PhoneNO { get; init; }
    public string? RequestDescription { get; init; }
    public bool IsEmailSend { get; init; }
    public string? JiraIssueId { get; init; }
    public string? JiraIssueKey { get; init; }
    public string? JiraIssueUrl { get; init; }
    public string? JiraRawResponse { get; init; }
    public bool JiraSuccess { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Persists support tickets in the tenant DB. Creates support.SupportTickets on first use.
/// </summary>
public sealed class SupportTicketStore
{
    private readonly ITenantConnectionProvider _connectionProvider;

    public SupportTicketStore(ITenantConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task InsertAsync(SupportTicketInsertRequest entry, CancellationToken cancellationToken)
    {
        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string is not available.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureTableAsync(connection, cancellationToken);

        const string sql = """
            INSERT INTO support.SupportTickets (
                Id, TenantId, UserId, CallerEmail,
                SupportCategory, Priorty, PreferredContact, PhoneNO, RequestDescription, IsEmailSend,
                JiraIssueId, JiraIssueKey, JiraIssueUrl, JiraRawResponse, JiraSuccess,
                CreatedAtUtc)
            VALUES (
                @Id, @TenantId, @UserId, @CallerEmail,
                @SupportCategory, @Priorty, @PreferredContact, @PhoneNO, @RequestDescription, @IsEmailSend,
                @JiraIssueId, @JiraIssueKey, @JiraIssueUrl, @JiraRawResponse, @JiraSuccess,
                @CreatedAtUtc)
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", entry.Id);
        command.Parameters.AddWithValue("@TenantId", entry.TenantId);
        command.Parameters.AddWithValue("@UserId", (object?)entry.UserId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CallerEmail", (object?)entry.CallerEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("@SupportCategory", (object?)entry.SupportCategory ?? DBNull.Value);
        command.Parameters.AddWithValue("@Priorty", (object?)entry.Priorty ?? DBNull.Value);
        command.Parameters.AddWithValue("@PreferredContact", (object?)entry.PreferredContact ?? DBNull.Value);
        command.Parameters.AddWithValue("@PhoneNO", (object?)entry.PhoneNO ?? DBNull.Value);
        command.Parameters.AddWithValue("@RequestDescription", (object?)Truncate(entry.RequestDescription, 1000) ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsEmailSend", entry.IsEmailSend);
        command.Parameters.AddWithValue("@JiraIssueId", (object?)entry.JiraIssueId ?? DBNull.Value);
        command.Parameters.AddWithValue("@JiraIssueKey", (object?)entry.JiraIssueKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@JiraIssueUrl", (object?)entry.JiraIssueUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("@JiraRawResponse", (object?)entry.JiraRawResponse ?? DBNull.Value);
        command.Parameters.AddWithValue("@JiraSuccess", entry.JiraSuccess);
        command.Parameters.AddWithValue("@CreatedAtUtc", entry.CreatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'support')
                EXEC(N'CREATE SCHEMA support');

            IF OBJECT_ID(N'support.SupportTickets', N'U') IS NULL
            BEGIN
                CREATE TABLE support.SupportTickets (
                    Id                  uniqueidentifier NOT NULL CONSTRAINT PK_SupportTickets PRIMARY KEY,
                    TenantId            uniqueidentifier NOT NULL,
                    UserId              uniqueidentifier NULL,
                    CallerEmail         nvarchar(256) NULL,
                    SupportCategory     nvarchar(256) NULL,
                    Priorty             nvarchar(64) NULL,
                    PreferredContact    nvarchar(64) NULL,
                    PhoneNO             nvarchar(64) NULL,
                    RequestDescription  nvarchar(1000) NULL,
                    IsEmailSend         bit NOT NULL CONSTRAINT DF_SupportTickets_IsEmailSend DEFAULT(0),
                    JiraIssueId         nvarchar(64) NULL,
                    JiraIssueKey        nvarchar(64) NULL,
                    JiraIssueUrl        nvarchar(512) NULL,
                    JiraRawResponse     nvarchar(max) NULL,
                    JiraSuccess         bit NOT NULL CONSTRAINT DF_SupportTickets_JiraSuccess DEFAULT(0),
                    CreatedAtUtc        datetime2 NOT NULL
                );

                CREATE INDEX IX_SupportTickets_TenantId_CreatedAtUtc
                    ON support.SupportTickets (TenantId, CreatedAtUtc DESC);
            END
            ELSE
            BEGIN
                -- Existing table created with NeedHelp: rename column to SupportCategory.
                IF COL_LENGTH(N'support.SupportTickets', N'NeedHelp') IS NOT NULL
                   AND COL_LENGTH(N'support.SupportTickets', N'SupportCategory') IS NULL
                BEGIN
                    EXEC sp_rename N'support.SupportTickets.NeedHelp', N'SupportCategory', N'COLUMN';
                END
            END
            """;

        try
        {
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2714 or 1913 or 2705 or 2627)
        {
            // Race: another request created the table/index — safe to continue.
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;
        return value[..maxLength];
    }
}
