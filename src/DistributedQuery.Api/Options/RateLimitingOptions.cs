using System.ComponentModel.DataAnnotations;

namespace DistributedQuery.Api.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 10_000)]
    public int MaxConcurrentRequests { get; init; } = 200;

    [Range(0, 10_000)]
    public int QueueLimit { get; init; } = 50;
}
