using System.Diagnostics;
using Serilog;

namespace SaaSApp.Api.Middleware;

public sealed class RequestPerformanceLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Serilog.ILogger _logger;
    private readonly int _slowRequestThresholdMs;

    public RequestPerformanceLoggingMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _logger = Log.ForContext<RequestPerformanceLoggingMiddleware>();
        _slowRequestThresholdMs = configuration.GetValue<int?>("Performance:SlowRequestThresholdMs") ?? 1000;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds < _slowRequestThresholdMs)
        {
            return;
        }

        _logger.Warning(
            "Slow HTTP request detected: {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (threshold {ThresholdMs}ms)",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            _slowRequestThresholdMs);
    }
}
