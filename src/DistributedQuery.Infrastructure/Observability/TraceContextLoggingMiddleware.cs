using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Infrastructure.Observability;

public sealed class TraceContextLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TraceContextLoggingMiddleware> _logger;

    public TraceContextLoggingMiddleware(RequestDelegate next, ILogger<TraceContextLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using (TraceContextPropagator.BeginLogScope(_logger))
        {
            await _next(context).ConfigureAwait(false);
        }
    }
}

public static class TraceContextLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseTraceContextLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<TraceContextLoggingMiddleware>();
}
