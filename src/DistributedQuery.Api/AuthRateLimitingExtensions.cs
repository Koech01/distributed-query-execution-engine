using System.Threading.RateLimiting;
using DistributedQuery.Api.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Api;

public static class AuthRateLimitingExtensions
{
    public const string AuthIpPolicy = "auth-ip";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthRateLimitOptions>()
            .Bind(configuration.GetSection(AuthRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthIpPolicy, CreateAuthIpLimiter);
        });

        return services;
    }

    private static RateLimitPartition<string> CreateAuthIpLimiter(HttpContext httpContext)
    {
        var rateLimitOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<AuthRateLimitOptions>>()
            .Value;

        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.PermitLimitPerIp,
                Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    }
}
