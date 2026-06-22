using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Infrastructure.Observability;

public static class TraceContextPropagator
{
    public static void InjectHttpHeaders(HttpRequestMessage request, Activity? activity = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        activity ??= Activity.Current;
        if (activity is null || string.IsNullOrWhiteSpace(activity.Id))
        {
            return;
        }

        request.Headers.Remove("traceparent");
        request.Headers.Remove("tracestate");
        request.Headers.TryAddWithoutValidation("traceparent", activity.Id);

        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            request.Headers.TryAddWithoutValidation("tracestate", activity.TraceStateString);
        }
    }

    public static ActivityContext ExtractHttpContext(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var traceParent = request.Headers["traceparent"].FirstOrDefault();
        var traceState = request.Headers["tracestate"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(traceParent) &&
            ActivityContext.TryParse(traceParent, traceState, out var context))
        {
            return context;
        }

        return default;
    }

    public static IDisposable BeginLogScope(ILogger logger, Activity? activity = null)
    {
        activity ??= Activity.Current;
        if (activity is null)
        {
            return NoopScope.Instance;
        }

        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["traceId"] = activity.TraceId.ToString(),
            ["spanId"] = activity.SpanId.ToString()
        }) ?? NoopScope.Instance;
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
        }
    }
}
