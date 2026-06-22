namespace DistributedQuery.Infrastructure.Discovery;

public sealed class ConsulOptions
{
    public const string SectionName = "Consul";

    public string Address { get; init; } = "http://localhost:8500";
    public string? Token { get; init; }
    public string ServiceName { get; init; } = "distributed-query-worker";
    public int NodeCacheTtlSeconds { get; init; } = 5;
}
