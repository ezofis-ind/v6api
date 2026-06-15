using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Infrastructure.Options;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>POST start payload to Python AP Agent only. Move-next is handled inside Python.</summary>
public sealed class ApAgentPythonPipelineService : IApAgentPythonPipelineService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITenantConnectionStringResolver _connectionStringResolver;
    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly JobExecutionContext _jobContext;
    private readonly IOptions<ApAgentOptions> _options;
    private readonly ILogger<ApAgentPythonPipelineService> _logger;

    public ApAgentPythonPipelineService(
        IHttpClientFactory httpClientFactory,
        ITenantConnectionStringResolver connectionStringResolver,
        ITenantConnectionProvider connectionProvider,
        JobExecutionContext jobContext,
        IOptions<ApAgentOptions> options,
        ILogger<ApAgentPythonPipelineService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _connectionStringResolver = connectionStringResolver;
        _connectionProvider = connectionProvider;
        _jobContext = jobContext;
        _options = options;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        ApAgentPythonJobArgs args,
        string? hangfireJobId = null,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation(
                "AP Agent Python pipeline disabled; skipping instance {InstanceId}.",
                args.InstanceId);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.PythonServiceUrl))
        {
            _logger.LogWarning(
                "ApAgent:PythonServiceUrl is not configured; skipping Python call for instance {InstanceId}.",
                args.InstanceId);
            return;
        }

        var connectionString = await _connectionStringResolver.GetConnectionStringAsync(args.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Tenant connection string not found for {args.TenantId:D}.");

        _connectionProvider.SetConnectionString(connectionString);
        _jobContext.Set(args.TenantId, args.UserId);

        try
        {
            var requestBody = BuildPythonRequestBody(args, hangfireJobId, options);
            await PostToPythonAsync(options, requestBody, cancellationToken);

            _logger.LogInformation(
                "AP Agent Python call finished for instance {InstanceId}.",
                args.InstanceId);
        }
        finally
        {
            _jobContext.Clear();
        }
    }

    /// <summary>Same start payload as workflow start, wrapped as { "startPayload": { ... } } for Python API.</summary>
    private static string BuildPythonRequestBody(
        ApAgentPythonJobArgs args,
        string? hangfireJobId,
        ApAgentOptions options)
    {
        if (string.IsNullOrWhiteSpace(args.StartPayloadJson))
            throw new InvalidOperationException("Start payload JSON is empty.");

        using var doc = System.Text.Json.JsonDocument.Parse(args.StartPayloadJson);
        if (ApAgentStartPayloadJson.TryGetNestedStartPayload(doc.RootElement, out _))
            return args.StartPayloadJson;

        var inner = ApAgentStartPayloadJson.UnwrapInner(args.StartPayloadJson);
        if (!string.IsNullOrWhiteSpace(hangfireJobId))
        {
            inner = ApAgentStartPayloadJson.EnrichWithJobTracking(
                inner,
                args.WorkflowId,
                args.InstanceId,
                hangfireJobId,
                options.ApiBaseUrl);
        }

        return ApAgentStartPayloadJson.WrapForPythonApi(inner);
    }

    private async Task PostToPythonAsync(
        ApAgentOptions options,
        string requestBody,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(ApAgentPythonPipelineService));
        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(Math.Max(1, options.TimeoutMinutes)));

        _logger.LogInformation("Posting start payload to Python AP Agent at {Url}", options.PythonServiceUrl);

        using var response = await client.PostAsync(options.PythonServiceUrl, content, timeoutCts.Token);
        var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Python AP Agent returned {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("Python AP Agent returned an empty response body.");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
