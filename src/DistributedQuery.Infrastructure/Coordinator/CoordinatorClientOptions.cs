using System.ComponentModel.DataAnnotations;

namespace DistributedQuery.Infrastructure.Coordinator;

public sealed class CoordinatorClientOptions
{
    public const string SectionName = "CoordinatorClient";

    [Required]
    public string BaseUrl { get; init; } = "http://localhost:5200";

    [Range(1, 300_000)]
    public int RequestTimeoutMs { get; init; } = 120_000;
}
