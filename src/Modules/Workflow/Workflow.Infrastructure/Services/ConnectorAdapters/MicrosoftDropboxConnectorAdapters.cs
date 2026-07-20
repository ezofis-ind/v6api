using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services.ConnectorAdapters;

internal abstract class MicrosoftGraphConnectorAdapterBase : ConnectorProviderAdapterBase
{
    protected MicrosoftGraphConnectorAdapterBase(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public override bool SupportsFiles => true;
    public override bool SupportsGmail => false;

    public override string BuildAuthorizeUrl(ConnectorProviderConfig config, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = config.RedirectUri,
            ["response_type"] = "code",
            ["response_mode"] = "query",
            ["state"] = state,
            ["scope"] = config.Scopes
        };
        return AppendQuery(config.AuthUrl, query);
    }

    public override async Task<ConnectorOAuthTokenResult> ExchangeCodeAsync(
        ConnectorProviderConfig config, string code, CancellationToken cancellationToken = default)
    {
        var token = await base.ExchangeCodeAsync(config, code, cancellationToken);
        var email = await TryGetGraphEmailAsync(token.AccessToken, cancellationToken);
        return token with { ExternalAccountEmail = email };
    }

    public override async Task<IReadOnlyList<(string Path, string Name, bool IsFolder, long? SizeBytes, DateTime? ModifiedAtUtc)>> ListFilesAsync(
        string accessToken, string? path, string? extraConfigJson, CancellationToken cancellationToken = default)
    {
        var drivePath = string.IsNullOrWhiteSpace(path) || path == "/"
            ? "https://graph.microsoft.com/v1.0/me/drive/root/children"
            : $"https://graph.microsoft.com/v1.0/me/drive/root:/{path.Trim('/')}:/children";

        using var client = CreateClient();
        using var req = AuthorizedGet(drivePath, accessToken);
        using var response = await client.SendAsync(req, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OneDrive/Teams list failed ({(int)response.StatusCode}): {body}");

        var items = new List<(string, string, bool, long?, DateTime?)>();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var values))
            return items;

        foreach (var item in values.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString() ?? "item";
            var isFolder = item.TryGetProperty("folder", out _);
            var itemPath = item.TryGetProperty("parentReference", out var parent) && parent.TryGetProperty("path", out var pp)
                ? $"{pp.GetString()?.Replace("/drive/root:", "", StringComparison.OrdinalIgnoreCase)?.TrimStart('/')}/{name}".Trim('/')
                : name;
            long? size = item.TryGetProperty("size", out var s) ? s.GetInt64() : null;
            DateTime? modified = item.TryGetProperty("lastModifiedDateTime", out var m) && DateTime.TryParse(m.GetString(), out var dt)
                ? dt.ToUniversalTime()
                : null;
            items.Add((itemPath, name, isFolder, size, modified));
        }
        return items;
    }

    public override async Task UploadFileAsync(
        string accessToken, string path, string fileName, Stream content, string? contentType, string? extraConfigJson, CancellationToken cancellationToken = default)
    {
        var relative = string.IsNullOrWhiteSpace(path)
            ? fileName
            : $"{path.TrimEnd('/')}/{fileName}".TrimStart('/');
        var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{relative}:/content";

        using var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StreamContent(content);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        using var response = await client.SendAsync(req, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Graph upload failed ({(int)response.StatusCode}): {body}");
        }
    }

    public override async Task<(Stream Content, string ContentType, string FileName)> DownloadFileAsync(
        string accessToken, string path, string? extraConfigJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path is required.");
        var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{path.TrimStart('/')}:/content";

        using var client = CreateClient();
        using var req = AuthorizedGet(url, accessToken);
        using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Graph download failed ({(int)response.StatusCode}): {body}");
        }

        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = path.Split('/').LastOrDefault() ?? "download";
        return (ms, contentType, fileName);
    }

    private static async Task<string?> TryGetGraphEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(req, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("mail", out var mail) && !string.IsNullOrWhiteSpace(mail.GetString()))
            return mail.GetString();
        return doc.RootElement.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() : null;
    }
}

internal sealed class OneDriveConnectorAdapter : MicrosoftGraphConnectorAdapterBase
{
    public OneDriveConnectorAdapter(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }
    public override string ProviderCode => "ONEDRIVE";
}

internal sealed class TeamsConnectorAdapter : MicrosoftGraphConnectorAdapterBase
{
    public TeamsConnectorAdapter(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }
    public override string ProviderCode => "TEAMS";
}

internal sealed class DropboxConnectorAdapter : ConnectorProviderAdapterBase
{
    public DropboxConnectorAdapter(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public override string ProviderCode => "DROPBOX";
    public override bool SupportsFiles => true;
    public override bool SupportsGmail => false;

    public override string BuildAuthorizeUrl(ConnectorProviderConfig config, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = config.RedirectUri,
            ["response_type"] = "code",
            ["token_access_type"] = "offline",
            ["state"] = state
        };
        return AppendQuery(config.AuthUrl, query);
    }

    public override async Task<ConnectorOAuthTokenResult> ExchangeCodeAsync(
        ConnectorProviderConfig config, string code, CancellationToken cancellationToken = default)
    {
        var token = await base.ExchangeCodeAsync(config, code, cancellationToken);
        var email = await TryGetDropboxEmailAsync(token.AccessToken, cancellationToken);
        return token with { ExternalAccountEmail = email };
    }

    public override async Task<IReadOnlyList<(string Path, string Name, bool IsFolder, long? SizeBytes, DateTime? ModifiedAtUtc)>> ListFilesAsync(
        string accessToken, string? path, string? extraConfigJson, CancellationToken cancellationToken = default)
    {
        var folder = string.IsNullOrWhiteSpace(path) ? "" : (path.StartsWith('/') ? path : "/" + path);
        using var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/2/files/list_folder");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(new { path = folder }), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(req, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Dropbox list failed ({(int)response.StatusCode}): {body}");

        var items = new List<(string, string, bool, long?, DateTime?)>();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("entries", out var entries))
            return items;

        foreach (var e in entries.EnumerateArray())
        {
            var tag = e.TryGetProperty(".tag", out var t) ? t.GetString() : null;
            var name = e.GetProperty("name").GetString() ?? "item";
            var itemPath = e.TryGetProperty("path_display", out var pd) ? pd.GetString() ?? name : name;
            var isFolder = string.Equals(tag, "folder", StringComparison.OrdinalIgnoreCase);
            long? size = e.TryGetProperty("size", out var s) ? s.GetInt64() : null;
            DateTime? modified = e.TryGetProperty("server_modified", out var m) && DateTime.TryParse(m.GetString(), out var dt)
                ? dt.ToUniversalTime()
                : null;
            items.Add((itemPath, name, isFolder, size, modified));
        }
        return items;
    }

    public override async Task UploadFileAsync(
        string accessToken, string path, string fileName, Stream content, string? contentType, string? extraConfigJson, CancellationToken cancellationToken = default)
    {
        var dropboxPath = string.IsNullOrWhiteSpace(path)
            ? "/" + fileName
            : (path.StartsWith('/') ? path : "/" + path).TrimEnd('/') + "/" + fileName;

        using var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://content.dropboxapi.com/2/files/upload");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.TryAddWithoutValidation("Dropbox-API-Arg", JsonSerializer.Serialize(new
        {
            path = dropboxPath,
            mode = "overwrite",
            autorename = false,
            mute = false
        }));
        req.Content = new StreamContent(content);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.SendAsync(req, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Dropbox upload failed ({(int)response.StatusCode}): {body}");
        }
    }

    public override async Task<(Stream Content, string ContentType, string FileName)> DownloadFileAsync(
        string accessToken, string path, string? extraConfigJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path is required.");
        var dropboxPath = path.StartsWith('/') ? path : "/" + path;

        using var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://content.dropboxapi.com/2/files/download");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.TryAddWithoutValidation("Dropbox-API-Arg", JsonSerializer.Serialize(new { path = dropboxPath }));
        using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Dropbox download failed ({(int)response.StatusCode}): {body}");
        }

        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        var fileName = dropboxPath.Split('/').LastOrDefault() ?? "download";
        return (ms, "application/octet-stream", fileName);
    }

    private static async Task<string?> TryGetDropboxEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/2/users/get_current_account");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent("null", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(req, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("email", out var email) ? email.GetString() : null;
    }
}
