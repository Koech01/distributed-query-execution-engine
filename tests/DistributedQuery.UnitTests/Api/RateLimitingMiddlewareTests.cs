using DistributedQuery.Api.Middleware;
using DistributedQuery.Api.Options;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.Api;

public sealed class RateLimitingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Returns429_WhenQueueIsFull()
    {
        var limiter = new RequestRateLimiter(
            Options.Create(new RateLimitingOptions
            {
                MaxConcurrentRequests = 1,
                QueueLimit = 0
            }),
            NullLogger<RequestRateLimiter>.Instance);

        var gate = new SemaphoreSlim(0);
        var middleware = new RateLimitingMiddleware(
            async _ =>
            {
                await gate.WaitAsync();
            },
            limiter,
            NullLogger<RateLimitingMiddleware>.Instance);

        var firstContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var firstRequest = middleware.InvokeAsync(firstContext);

        await Task.Delay(50);

        var secondContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await middleware.InvokeAsync(secondContext);

        secondContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);

        gate.Release();
        await firstRequest;
        limiter.Dispose();
    }
}
