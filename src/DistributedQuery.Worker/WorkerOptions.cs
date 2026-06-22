using System.ComponentModel.DataAnnotations;

namespace DistributedQuery.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    [Required]
    [MinLength(1)]
    public string NodeId { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int GrpcPort { get; init; } = 5100;

    [Range(1, 65535)]
    public int HealthPort { get; init; } = 5101;

    public string Address { get; init; } = "127.0.0.1";

    [Required]
    public IReadOnlyList<int> ShardIndices { get; init; } = Array.Empty<int>();

    public string Version { get; init; } = "1.0.0";

    [Required]
    public WorkerExecutionOptions Execution { get; init; } = new();

    [Required]
    public Dictionary<string, string> Shards { get; init; } = new(StringComparer.Ordinal);

    [Required]
    public WorkerConsulRegistrationOptions Consul { get; init; } = new();
}

public sealed class WorkerExecutionOptions
{
    [Range(1, 10_000)]
    public int StreamChunkSize { get; init; } = 500;

    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; init; } = 25;

    [Range(1, 1_000)]
    public int MaxConcurrentQueries { get; init; } = 10;

    [Range(1, 300)]
    public int DrainTimeoutSeconds { get; init; } = 15;
}

public sealed class WorkerConsulRegistrationOptions
{
    public bool Enabled { get; init; }

    [Range(1, 300)]
    public int HealthCheckIntervalSeconds { get; init; } = 10;

    [Range(1, 60)]
    public int HealthCheckTimeoutSeconds { get; init; } = 5;

    [Range(5, 600)]
    public int DeregisterCriticalServiceAfterSeconds { get; init; } = 30;
}
