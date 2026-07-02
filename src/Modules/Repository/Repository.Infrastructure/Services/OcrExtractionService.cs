using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Repository.Infrastructure.Options;

namespace SaaSApp.Repository.Infrastructure.Services;

public sealed class OcrExtractionService : IOcrExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly RepositoryOcrOptions _options;
    private readonly ILogger<OcrExtractionService> _logger;

    public OcrExtractionService(
        HttpClient httpClient,
        IOptions<RepositoryOcrOptions> options,
        ILogger<OcrExtractionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMinutes(Math.Clamp(_options.TimeoutMinutes, 1, 30));
    }

    public async Task<OcrExtractionResult> ExtractFromFileAsync(
        byte[] fileBytes,
        IReadOnlyList<string> parameters,
        IReadOnlyList<Dictionary<string, IReadOnlyList<string>>>? tableParameters = null,
        string? pageNo = null,
        string? ocrType = null,
        string? validateType = null,
        string? filename = null,
        Guid? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        if (fileBytes.Length == 0)
            throw new ArgumentException("File is empty.");

        if (parameters.Count == 0)
            throw new ArgumentException("At least one OCR parameter is required in fields.");

        var apiUrl = _options.UploadForOcrApiUrl?.Trim();
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new InvalidOperationException(
                "Repository:Ocr:UploadForOcrApiUrl is not configured in appsettings.");
        }

        var resolvedPageNo = ResolvePageNo(pageNo, _options.PageNo);
        var resolvedOcrType = ResolveOcrType(ocrType, _options.OcrType);
        var resolvedValidateType = ResolveValidateType(validateType, _options.ValidateType);

        var payload = BuildPayload(
            fileBytes,
            parameters,
            tableParameters,
            resolvedPageNo,
            resolvedOcrType,
            resolvedValidateType,
            filename,
            repositoryId);

        _logger.LogInformation(
            "Calling OCR API {Url} with {ParameterCount} parameters ({Parameters}), pageno={PageNo}, ocrtype={OcrType}, file size {FileSize} bytes",
            apiUrl,
            parameters.Count,
            string.Join("; ", parameters),
            resolvedPageNo,
            resolvedOcrType,
            fileBytes.Length);

        var rawJson = await PostAsync(apiUrl, payload, cancellationToken);

        if (IsNoTextExtractedError(rawJson)
            && !string.Equals(resolvedOcrType, "tesseract", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "OCR returned no text with ocrtype={OcrType}; retrying with tesseract",
                resolvedOcrType);

            payload["ocrtype"] = "tesseract";
            rawJson = await PostAsync(apiUrl, payload, cancellationToken);
        }

        var fieldList = OcrResultParser.TryParseFieldList(rawJson);
        return new OcrExtractionResult(rawJson, fieldList);
    }

    private async Task<string> PostAsync(string apiUrl, Dictionary<string, object> payload, CancellationToken cancellationToken)
    {
        var logPayload = new Dictionary<string, object>(payload);
        if (logPayload.TryGetValue("filepath", out var filepath) && filepath is string filepathText && filepathText.Length > 80)
            logPayload["filepath"] = filepathText[..80] + "...";
        if (logPayload.TryGetValue("file", out var file) && file is string fileText && fileText.Length > 80)
            logPayload["file"] = fileText[..80] + "...";

        _logger.LogDebug("OCR API request payload: {Payload}", JsonSerializer.Serialize(logPayload));

        using var response = await _httpClient.PostAsJsonAsync(apiUrl, payload, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OCR API returned {StatusCode}: {Body}", (int)response.StatusCode, rawJson);
            throw new InvalidOperationException(
                $"OCR API failed ({(int)response.StatusCode}): {Truncate(rawJson, 500)}");
        }

        return rawJson;
    }

    private static Dictionary<string, object> BuildPayload(
        byte[] fileBytes,
        IReadOnlyList<string> parameters,
        IReadOnlyList<Dictionary<string, IReadOnlyList<string>>>? tableParameters,
        string pageNo,
        string ocrType,
        string validateType,
        string? filename,
        Guid? repositoryId)
    {
        var base64 = Convert.ToBase64String(fileBytes);
        var payload = new Dictionary<string, object>
        {
            ["filepath"] = base64,
            ["file"] = base64,
            ["pageno"] = pageNo,
            ["ocrtype"] = ocrType,
            ["validatetype"] = validateType,
            ["parameters"] = parameters,
            ["tableparameters"] = tableParameters ?? Array.Empty<Dictionary<string, IReadOnlyList<string>>>()
        };

        if (!string.IsNullOrWhiteSpace(filename))
            payload["filename"] = filename.Trim();

        if (repositoryId.HasValue && repositoryId.Value != Guid.Empty)
            payload["repositoryId"] = repositoryId.Value.ToString("D");

        return payload;
    }

    private static bool IsNoTextExtractedError(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;

        return rawJson.Contains("no text extracted", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePageNo(string? pageNo, string defaultPageNo)
    {
        var value = string.IsNullOrWhiteSpace(pageNo) ? defaultPageNo : pageNo.Trim();
        if (IsPlaceholderValue(value))
            value = defaultPageNo.Trim();

        if (string.Equals(value, "-1", StringComparison.Ordinal))
            return value;

        if (value.Contains('-', StringComparison.Ordinal))
        {
            var parts = value.Split('-', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && int.TryParse(parts[0], out _)
                && int.TryParse(parts[1], out _))
            {
                return value;
            }

            return ResolvePageNo(null, defaultPageNo);
        }

        if (int.TryParse(value, out var page) && page > 0)
            return value;

        return int.TryParse(defaultPageNo, out var defaultPage) && defaultPage > 0
            ? defaultPage.ToString()
            : "1";
    }

    private static string ResolveOcrType(string? ocrType, string defaultOcrType)
    {
        var value = string.IsNullOrWhiteSpace(ocrType) ? defaultOcrType : ocrType.Trim();
        if (IsPlaceholderValue(value))
            return defaultOcrType.Trim();

        return value;
    }

    private static string ResolveValidateType(string? validateType, string defaultValidateType)
    {
        var value = string.IsNullOrWhiteSpace(validateType) ? defaultValidateType : validateType.Trim();
        if (IsPlaceholderValue(value))
            return defaultValidateType.Trim();

        return value;
    }

    private static bool IsPlaceholderValue(string value) =>
        string.Equals(value, "string", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}
