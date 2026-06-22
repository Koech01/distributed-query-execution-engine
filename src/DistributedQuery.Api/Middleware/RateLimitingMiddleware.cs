using System.Diagnostics;
using DistributedQuery.Api.Options;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Api.Middleware;

public sealed class RateLimitingMiddleware
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.RateLimitingMiddleware");

    private readonly RequestDelegate _next;
    private readonly RequestRateLimiter _rateLimiter;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        RequestRateLimiter rateLimiter,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var activity = ActivitySource.StartActivity("api.rate_limit.acquire", ActivityKind.Internal);

        var lease = await _rateLimiter.TryAcquireAsync(context.RequestAborted).ConfigureAwait(false);
        if (lease is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "rate_limited");
            _logger.LogWarning("Rate limit rejected request {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "1";
            await context.Response.WriteAsJsonAsync(
                new { error = "rate_limited", message = "Too many concurrent requests. Try again shortly." },
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await using (lease)
        {
            await _next(context).ConfigureAwait(false);
        }
    }
}

public sealed class RequestRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly SemaphoreSlim? _queueGate;
    private readonly ILogger<RequestRateLimiter> _logger;
    private int _activeRequests;

    public RequestRateLimiter(IOptions<RateLimitingOptions> options, ILogger<RequestRateLimiter> logger)
    {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _concurrencyGate = new SemaphoreSlim(value.MaxConcurrentRequests, value.MaxConcurrentRequests);
        _queueGate = value.QueueLimit > 0
            ? new SemaphoreSlim(value.QueueLimit, value.QueueLimit)
            : null;
    }

    public int ActiveRequests => Volatile.Read(ref _activeRequests);

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        var acquiredImmediately = await _concurrencyGate
            .WaitAsync(0, cancellationToken)
            .ConfigureAwait(false);

        if (!acquiredImmediately)
        {
            if (_queueGate is null)
            {
                return null;
            }

            var queued = await _queueGate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
            if (!queued)
            {
                return null;
            }

            try
            {
                await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _queueGate.Release();
            }
        }

        Interlocked.Increment(ref _activeRequests);
        _logger.LogDebug("Rate limiter granted lease. Active={Active}", _activeRequests);
        return new RateLimitLease(this);
    }

    private void Release()
    {
        Interlocked.Decrement(ref _activeRequests);
        _concurrencyGate.Release();
        _logger.LogDebug("Rate limiter released lease. Active={Active}", _activeRequests);
    }

    public void Dispose()
    {
        _concurrencyGate.Dispose();
        _queueGate?.Dispose();
    }

    private sealed class RateLimitLease : IAsyncDisposable
    {
        private readonly RequestRateLimiter _owner;
        private int _disposed;

        public RateLimitLease(RequestRateLimiter owner) => _owner = owner;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
