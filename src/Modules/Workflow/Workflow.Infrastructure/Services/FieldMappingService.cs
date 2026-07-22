using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>Proxies field-mapping requests to the Python field-mapping API.</summary>
public sealed class FieldMappingService : IFieldMappingService
{
    public const string PythonFieldMappingUrl = "http://52.172.32.88:8095/api/v1/field-mapping";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FieldMappingService> _logger;

    public FieldMappingService(IHttpClientFactory httpClientFactory, ILogger<FieldMappingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<JsonElement> MapFieldsAsync(
        FieldMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ExcelSheets.Count == 0)
            throw new ArgumentException("excelSheets is required.");
        if (request.HeaderFields.Count == 0 && request.LineItemFields.Count == 0)
            throw new ArgumentException("At least one of headerFields or lineItemFields is required.");

        var body = JsonSerializer.Serialize(request, SerializerOptions);
        var client = _httpClientFactory.CreateClient(nameof(FieldMappingService));

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, PythonFieldMappingUrl) { Content = content };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogInformation(
            "Calling Python field-mapping at {Url} (sheets={SheetCount}, headerFields={HeaderCount}, lineItemFields={LineCount})",
            PythonFieldMappingUrl,
            request.ExcelSheets.Count,
            request.HeaderFields.Count,
            request.LineItemFields.Count);

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Python field-mapping returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                Truncate(responseText, 2000));
            throw new InvalidOperationException(
                $"Field-mapping service returned {(int)response.StatusCode}: {Truncate(responseText, 500)}");
        }

        if (string.IsNullOrWhiteSpace(responseText))
            return JsonDocument.Parse("null").RootElement.Clone();

        using var doc = JsonDocument.Parse(responseText);
        return doc.RootElement.Clone();
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value ?? string.Empty;
        return value[..max] + "…";
    }
}
