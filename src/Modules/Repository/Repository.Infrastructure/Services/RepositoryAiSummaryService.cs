using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Repository.Infrastructure.Options;

namespace SaaSApp.Repository.Infrastructure.Services;

public sealed class RepositoryAiSummaryService : IRepositoryAiSummaryService
{
    private readonly HttpClient _httpClient;
    private readonly RepositoryAiSummaryOptions _options;
    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly IStaticRepositoryProvisioner _provisioner;
    private readonly IRepositoryItemQueryService _items;
    private readonly ILogger<RepositoryAiSummaryService> _logger;

    public RepositoryAiSummaryService(
        HttpClient httpClient,
        IOptions<RepositoryAiSummaryOptions> options,
        ITenantConnectionProvider connectionProvider,
        IStaticRepositoryProvisioner provisioner,
        IRepositoryItemQueryService items,
        ILogger<RepositoryAiSummaryService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _connectionProvider = connectionProvider;
        _provisioner = provisioner;
        _items = items;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMinutes(Math.Clamp(_options.TimeoutMinutes, 1, 30));
    }

    public async Task<AiSummaryResult> GetOrGenerateAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var repo = await _provisioner.GetRepositoryAsync(repositoryId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Repository not found.");

        if (!RepositorySqlHelper.IsValidItemsTableName(repo.ItemsTableName))
            throw new InvalidOperationException("Invalid items table.");

        var table = RepositorySqlHelper.QualifiedItemsTable(repo.ItemsTableName);
        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        string? filePath;
        string? summaryJson;
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var selectSql = $"""
                SELECT FilePath, SummaryJson
                FROM {table}
                WHERE Id = @ItemId AND RepositoryId = @RepositoryId AND IsDeleted = 0;
                """;

            await using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@ItemId", itemId);
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException("Repository item not found.");

            filePath = reader.IsDBNull(0) ? null : reader.GetString(0);
            summaryJson = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        if (!string.IsNullOrWhiteSpace(summaryJson))
            return new AiSummaryResult(summaryJson, WasGenerated: false);

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Repository item does not have a file path.");

        var apiUrl = _options.ApiUrl?.Trim();
        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new HttpRequestException("Repository:AiSummary:ApiUrl is not configured.");

        _logger.LogInformation(
            "Calling AI summary API {Url} for tenant {TenantId}, repository {RepositoryId}, item {ItemId}",
            apiUrl,
            tenantId,
            repositoryId,
            itemId);

        var normalizedFilePath = NormalizeSummaryFilePath(filePath);
        var useBase64Payload = ContainsNonAsciiPath(filePath);
        HttpResponseMessage response;
        string responseBody;

        if (useBase64Payload)
        {
            _logger.LogInformation(
                "AI summary item {ItemId} has non-ASCII blob path; sending file bytes to Python instead of blob path.",
                itemId);
            (response, responseBody) = await PostSummaryWithFileBytesAsync(
                apiUrl,
                tenantId,
                repositoryId,
                itemId,
                cancellationToken);
        }
        else
        {
            (response, responseBody) = await PostSummaryRequestAsync(
                apiUrl,
                tenantId,
                filePath,
                cancellationToken: cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound
                && !string.Equals(normalizedFilePath, filePath, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "AI summary API returned 404 for item {ItemId} with raw filepath; retrying with normalized filepath. Raw={RawFilePath} Normalized={NormalizedFilePath}",
                    itemId,
                    filePath,
                    normalizedFilePath);

                response.Dispose();
                (response, responseBody) = await PostSummaryRequestAsync(
                    apiUrl,
                    tenantId,
                    normalizedFilePath,
                    cancellationToken: cancellationToken);
            }

            if (!response.IsSuccessStatusCode && IsBlobNotFoundResponse(response, responseBody))
            {
                _logger.LogWarning(
                    "AI summary API could not read blob for item {ItemId}; retrying with file bytes. FilePath={FilePath}",
                    itemId,
                    filePath);

                response.Dispose();
                (response, responseBody) = await PostSummaryWithFileBytesAsync(
                    apiUrl,
                    tenantId,
                    repositoryId,
                    itemId,
                    cancellationToken);
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "AI summary API returned {StatusCode} for item {ItemId}: {Body}",
                (int)response.StatusCode,
                itemId,
                Truncate(responseBody, 500));
            throw new HttpRequestException(
                $"AI summary API failed ({(int)response.StatusCode}): {Truncate(responseBody, 500)}",
                null,
                response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(responseBody))
            throw new HttpRequestException("AI summary API returned an empty response.");

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var updateSql = $"""
                UPDATE {table}
                SET SummaryJson = @SummaryJson, ModifiedAtUtc = SYSUTCDATETIME()
                WHERE Id = @ItemId AND RepositoryId = @RepositoryId AND IsDeleted = 0;
                """;

            await using var command = new SqlCommand(updateSql, connection);
            command.Parameters.AddWithValue("@SummaryJson", responseBody);
            command.Parameters.AddWithValue("@ItemId", itemId);
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                throw new KeyNotFoundException("Repository item not found.");
        }

        return new AiSummaryResult(responseBody, WasGenerated: true);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    private async Task<(HttpResponseMessage Response, string Body)> PostSummaryRequestAsync(
        string apiUrl,
        Guid tenantId,
        string filePath,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        object payload = string.IsNullOrWhiteSpace(fileName)
            ? new { tenantId, filepath = filePath }
            : new { tenantId, filepath = filePath, filename = fileName };

        var response = await _httpClient.PostAsJsonAsync(apiUrl, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response, body);
    }

    private async Task<(HttpResponseMessage Response, string Body)> PostSummaryWithFileBytesAsync(
        string apiUrl,
        Guid tenantId,
        Guid repositoryId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var fileContent = await _items.OpenItemFileAsync(repositoryId, tenantId, itemId, cancellationToken)
            ?? throw new FileNotFoundException("Repository file not found in storage for AI summary.");

        using (fileContent.Stream)
        {
            using var buffer = new MemoryStream();
            await fileContent.Stream.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length == 0)
                throw new InvalidOperationException("Repository file is empty.");

            var base64 = Convert.ToBase64String(buffer.ToArray());
            var response = await _httpClient.PostAsJsonAsync(
                apiUrl,
                new
                {
                    tenantId,
                    filepath = base64,
                    file = base64,
                    filename = fileContent.FileName
                },
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response, body);
        }
    }

    private static bool ContainsNonAsciiPath(string filePath) =>
        filePath.Any(static c => c > 127);

    private static bool IsBlobNotFoundResponse(HttpResponseMessage response, string body) =>
        response.StatusCode == HttpStatusCode.NotFound
        && body.Contains("Blob not found", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSummaryFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        var normalized = filePath
            .Replace('\\', '/')
            .TrimStart('/')
            .Normalize(NormalizationForm.FormC);

        return string.Join(
            '/',
            normalized
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Normalize(NormalizationForm.FormC)));
    }
}
