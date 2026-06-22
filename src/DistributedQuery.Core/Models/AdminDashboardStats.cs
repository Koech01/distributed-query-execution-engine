namespace DistributedQuery.Core.Models;

public sealed record AdminDashboardStats(
    int ActiveQueries,
    int HealthyWorkers,
    int TotalWorkers,
    long PlanCacheEntries,
    long ResultCacheEntries,
    long AsyncQueryStatusEntries,
    DateTimeOffset GeneratedAt);
