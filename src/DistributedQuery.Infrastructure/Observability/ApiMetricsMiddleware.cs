using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DistributedQuery.Infrastructure.Observability;

public sealed class ApiMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public ApiMetricsMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        DqeeMetrics.ApiActiveRequests.Add(1);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            DqeeMetrics.ApiActiveRequests.Add(-1);

            var statusCode = context.Response.StatusCode.ToString();
            DqeeMetrics.ApiRequestsTotal.Add(1, new KeyValuePair<string, object?>("method", method), new KeyValuePair<string, object?>("status_code", statusCode));
            DqeeMetrics.ApiRequestDurationSeconds.Record(
                stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("status_code", statusCode));
        }
    }
}

public static class ApiMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseApiMetrics(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiMetricsMiddleware>();
}
