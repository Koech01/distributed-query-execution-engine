using System.ComponentModel.DataAnnotations;
using DistributedQuery.Core.Models;

namespace DistributedQuery.Coordinator;

public sealed class CoordinatorOptions
{
    public const string SectionName = "Coordinator";

    [Range(1, 300_000)]
    public int DefaultQueryTimeoutMs { get; init; } = 30_000;

    [Range(1, 300_000)]
    public int MaxQueryTimeoutMs { get; init; } = 120_000;

    [Range(1, 86_400)]
    public int PlanCacheTtlSeconds { get; init; } = 3_600;

    [Range(1, 86_400)]
    public int ResultCacheTtlSeconds { get; init; } = 300;

    [Required]
    public FanOutOptions FanOut { get; init; } = new();

    [Required]
    public FailurePolicy PartialFailurePolicy { get; init; } = FailurePolicy.BestEffort;

    [Range(0.0, 1.0)]
    public double MinimumShardCoverage { get; init; } = 0.8d;

    [Required]
    public ResilienceOptions Resilience { get; init; } = new();
}

public sealed class FanOutOptions
{
    [Range(1, 1_000)]
    public int MaxConcurrentWorkerCalls { get; init; } = 50;

    [Range(1, 300_000)]
    public int PerWorkerTimeoutMs { get; init; } = 25_000;
}

public sealed class ResilienceOptions
{
    [Range(0, 10)]
    public int RetryCount { get; init; } = 3;

    [Range(1, 60_000)]
    public int RetryBaseDelayMs { get; init; } = 100;

    [Range(1, 100)]
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    [Range(1, 600)]
    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 30;

    [Range(1, 600)]
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 15;
}
