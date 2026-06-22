using System.ComponentModel.DataAnnotations;

namespace DistributedQuery.Api.Options;

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "Authentication:RateLimit";

    [Range(1, 10_000)]
    public int PermitLimitPerIp { get; init; } = 10;

    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;
}
