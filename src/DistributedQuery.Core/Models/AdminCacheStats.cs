namespace DistributedQuery.Core.Models;

public sealed record AdminCacheStats(
    long PlanCacheEntries,
    long ResultCacheEntries,
    long AsyncQueryStatusEntries,
    DateTimeOffset GeneratedAt);
