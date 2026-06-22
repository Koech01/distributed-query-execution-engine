namespace DistributedQuery.Core.Models;

public sealed record WorkerHealthDashboard(
    IReadOnlyList<WorkerHealthEntry> Workers,
    int HealthyCount,
    int TotalCount,
    DateTimeOffset GeneratedAt);
