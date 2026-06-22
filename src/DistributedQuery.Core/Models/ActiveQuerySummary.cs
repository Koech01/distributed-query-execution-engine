namespace DistributedQuery.Core.Models;

public enum ActiveQueryKind
{
    Sync,
    Stream,
    Async
}

public sealed record ActiveQuerySummary(
    Guid QueryId,
    ActiveQueryKind Kind,
    string PlanHash,
    int SubQueryCount,
    DateTimeOffset StartedAt,
    bool CancellationRequested);
