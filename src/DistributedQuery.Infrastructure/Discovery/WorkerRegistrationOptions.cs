namespace DistributedQuery.Infrastructure.Discovery;

public sealed class WorkerRegistrationOptions
{
    public const string SectionName = "WorkerRegistration";

    public bool Enabled { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Address { get; set; } = "127.0.0.1";
    public int GrpcPort { get; set; } = 5100;
    public int HealthPort { get; set; } = 5101;
    public IReadOnlyList<int> ShardIndices { get; set; } = Array.Empty<int>();
    public string Version { get; set; } = "1.0.0";
    public int HealthCheckIntervalSeconds { get; set; } = 10;
    public int HealthCheckTimeoutSeconds { get; set; } = 5;
    public int DeregisterCriticalServiceAfterSeconds { get; set; } = 30;
}
