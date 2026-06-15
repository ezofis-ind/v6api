using System.Text.Json;

namespace SaaSApp.Api.Services;

/// <summary>
/// v5-compatible workflow transaction processing (<c>POST api/workflow/transaction</c>).
/// Full old behavior lives in v5 <c>ProcessTransactionFn</c>; this service can proxy to v5 or run a safe in-process subset.
/// </summary>
public interface ILegacyWorkflowTransactionService
{
    /// <summary>Raw JSON body as sent by the client (v5 ProcessData shape).</summary>
    Task<(int StatusCode, string ContentType, string Body)> ExecuteAsync(JsonElement body, HttpRequest httpRequest, CancellationToken cancellationToken);
}
