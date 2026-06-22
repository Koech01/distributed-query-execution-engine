namespace DistributedQuery.Infrastructure.Caching;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; init; } = "localhost:6379,abortConnect=false";
    public string InstanceName     { get; init; } = "dqee:";
    public int    PlanCacheTtlSeconds   { get; init; } = 3600;
    public int    ResultCacheTtlSeconds { get; init; } = 300;
    public bool   EnableCompression     { get; init; } = true;
    public int    CompressionThresholdBytes { get; init; } = 1024;
}
