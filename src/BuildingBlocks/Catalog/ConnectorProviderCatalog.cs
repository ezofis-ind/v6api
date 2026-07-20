using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaaSApp.Catalog.Entities;
using SaaSApp.Catalog.Persistence;

namespace SaaSApp.Catalog;

public sealed class ConnectorProviderCatalog : IConnectorProviderCatalog
{
    private const int SqlErrorInvalidObjectName = 208;

    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;

    public ConnectorProviderCatalog(IDbContextFactory<CatalogDbContext> catalogFactory)
    {
        _catalogFactory = catalogFactory;
    }

    public async Task EnsureSchemaAndSeedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            IF OBJECT_ID(N'[catalog].[ConnectorProviders]', N'U') IS NULL
            BEGIN
                CREATE TABLE [catalog].[ConnectorProviders] (
                    [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ConnectorProviders] PRIMARY KEY DEFAULT NEWID(),
                    [ProviderCode] NVARCHAR(64) NOT NULL,
                    [DisplayName] NVARCHAR(128) NOT NULL,
                    [ClientId] NVARCHAR(512) NOT NULL CONSTRAINT [DF_ConnectorProviders_ClientId] DEFAULT (N''),
                    [ClientSecret] NVARCHAR(1024) NOT NULL CONSTRAINT [DF_ConnectorProviders_ClientSecret] DEFAULT (N''),
                    [AuthUrl] NVARCHAR(1024) NOT NULL,
                    [TokenUrl] NVARCHAR(1024) NOT NULL,
                    [Scopes] NVARCHAR(2000) NOT NULL CONSTRAINT [DF_ConnectorProviders_Scopes] DEFAULT (N''),
                    [RedirectUri] NVARCHAR(1024) NOT NULL CONSTRAINT [DF_ConnectorProviders_RedirectUri] DEFAULT (N''),
                    [ExtraConfigJson] NVARCHAR(MAX) NULL,
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_ConnectorProviders_IsActive] DEFAULT (1),
                    [CreatedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ConnectorProviders_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
                    [ModifiedAtUtc] DATETIME2(3) NULL,
                    CONSTRAINT [UQ_ConnectorProviders_ProviderCode] UNIQUE ([ProviderCode])
                );
            END

            MERGE [catalog].[ConnectorProviders] AS t
            USING (VALUES
                (N'GCP', N'Google Cloud Storage',
                 N'https://accounts.google.com/o/oauth2/v2/auth',
                 N'https://oauth2.googleapis.com/token',
                 N'https://www.googleapis.com/auth/devstorage.read_write https://www.googleapis.com/auth/userinfo.email openid'),
                (N'GMAIL', N'Gmail',
                 N'https://accounts.google.com/o/oauth2/v2/auth',
                 N'https://oauth2.googleapis.com/token',
                 N'https://www.googleapis.com/auth/gmail.modify https://www.googleapis.com/auth/userinfo.email openid'),
                (N'OUTLOOK', N'Office 365 Outlook',
                 N'https://login.microsoftonline.com/common/oauth2/v2.0/authorize',
                 N'https://login.microsoftonline.com/common/oauth2/v2.0/token',
                 N'offline_access openid profile email Mail.ReadWrite User.Read'),
                (N'ONEDRIVE', N'Microsoft OneDrive',
                 N'https://login.microsoftonline.com/common/oauth2/v2.0/authorize',
                 N'https://login.microsoftonline.com/common/oauth2/v2.0/token',
                 N'offline_access openid profile email Files.ReadWrite.All User.Read'),
                (N'TEAMS', N'Microsoft Teams',
                 N'https://login.microsoftonline.com/common/oauth2/v2.0/authorize',
                 N'https://login.microsoftonline.com/common/oauth2/v2.0/token',
                 N'offline_access openid profile email Files.ReadWrite.All Sites.ReadWrite.All User.Read'),
                (N'DROPBOX', N'Dropbox',
                 N'https://www.dropbox.com/oauth2/authorize',
                 N'https://api.dropboxapi.com/oauth2/token',
                 N''),
                (N'QUICKBOOKS', N'QuickBooks',
                 N'https://appcenter.intuit.com/connect/oauth2',
                 N'https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer',
                 N'com.intuit.quickbooks.accounting openid profile email')
            ) AS s ([ProviderCode], [DisplayName], [AuthUrl], [TokenUrl], [Scopes])
            ON t.[ProviderCode] = s.[ProviderCode]
            WHEN MATCHED AND t.[ProviderCode] IN (N'GMAIL', N'OUTLOOK') THEN
                UPDATE SET [Scopes] = s.[Scopes], [ModifiedAtUtc] = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT ([Id], [ProviderCode], [DisplayName], [ClientId], [ClientSecret], [AuthUrl], [TokenUrl], [Scopes], [RedirectUri], [IsActive], [CreatedAtUtc])
                VALUES (NEWID(), s.[ProviderCode], s.[DisplayName], N'', N'', s.[AuthUrl], s.[TokenUrl], s.[Scopes], N'', 1, SYSUTCDATETIME());
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        // Migrate legacy QUICKBOOKS_EMAIL → QUICKBOOKS (or deactivate if both exist)
        cmd.CommandText = """
            IF EXISTS (SELECT 1 FROM [catalog].[ConnectorProviders] WHERE [ProviderCode] = N'QUICKBOOKS_EMAIL')
               AND NOT EXISTS (SELECT 1 FROM [catalog].[ConnectorProviders] WHERE [ProviderCode] = N'QUICKBOOKS')
            BEGIN
                UPDATE [catalog].[ConnectorProviders]
                SET [ProviderCode] = N'QUICKBOOKS',
                    [DisplayName] = N'QuickBooks',
                    [Scopes] = N'com.intuit.quickbooks.accounting openid profile email',
                    [ModifiedAtUtc] = SYSUTCDATETIME()
                WHERE [ProviderCode] = N'QUICKBOOKS_EMAIL';
            END
            ELSE IF EXISTS (SELECT 1 FROM [catalog].[ConnectorProviders] WHERE [ProviderCode] = N'QUICKBOOKS_EMAIL')
            BEGIN
                UPDATE [catalog].[ConnectorProviders]
                SET [IsActive] = 0, [ModifiedAtUtc] = SYSUTCDATETIME()
                WHERE [ProviderCode] = N'QUICKBOOKS_EMAIL';
            END
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectorProvider>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureSchemaAndSeedAsync(cancellationToken);
            await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
            return await context.ConnectorProviders
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayName)
                .ToListAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorInvalidObjectName)
        {
            return Array.Empty<ConnectorProvider>();
        }
    }

    public async Task<ConnectorProvider?> GetByCodeAsync(string providerCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
            return null;

        try
        {
            await EnsureSchemaAndSeedAsync(cancellationToken);
            await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
            var code = providerCode.Trim().ToUpperInvariant();
            return await context.ConnectorProviders
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProviderCode == code && p.IsActive, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorInvalidObjectName)
        {
            return null;
        }
    }

    public async Task UpsertCredentialsAsync(
        string providerCode,
        string clientId,
        string clientSecret,
        string redirectUri,
        string? scopes = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken);
        await using var context = await _catalogFactory.CreateDbContextAsync(cancellationToken);
        var code = providerCode.Trim().ToUpperInvariant();
        var row = await context.ConnectorProviders.FirstOrDefaultAsync(p => p.ProviderCode == code, cancellationToken);
        if (row == null)
            throw new InvalidOperationException($"Unknown provider code '{providerCode}'.");

        row.ClientId = clientId?.Trim() ?? string.Empty;
        row.ClientSecret = clientSecret?.Trim() ?? string.Empty;
        row.RedirectUri = redirectUri?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(scopes))
            row.Scopes = scopes.Trim();
        row.ModifiedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
