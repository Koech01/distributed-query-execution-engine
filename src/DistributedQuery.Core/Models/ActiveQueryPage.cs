namespace DistributedQuery.Core.Models;

public sealed record ActiveQueryPage(
    IReadOnlyList<ActiveQuerySummary> Queries,
    int TotalCount,
    int Limit,
    int Offset);
