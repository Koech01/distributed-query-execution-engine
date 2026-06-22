namespace DistributedQuery.Core.Models;

public enum WorkerProbeStatus
{
    Healthy,
    Unhealthy,
    Unreachable
}

public sealed record WorkerHealthEntry(
    string NodeId,
    string Address,
    int GrpcPort,
    int HealthPort,
    IReadOnlyList<int> Shards,
    string Version,
    WorkerProbeStatus LiveStatus,
    WorkerProbeStatus ReadyStatus,
    WorkerProbeStatus GrpcStatus,
    int? LiveLatencyMs,
    int? ReadyLatencyMs,
    int? GrpcLatencyMs,
    bool RegisteredInConsul);
