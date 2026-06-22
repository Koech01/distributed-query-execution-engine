namespace DistributedQuery.Core.Models;

public record QueryResult(
    Guid QueryId,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int RowCount,
    int TotalShards,
    int SuccessfulShards,
    IReadOnlyList<int> FailedShards,
    bool Degraded,
    string? DegradationReason,
    long ExecutionMs,
    bool FromCache
)
{
    public static QueryResult Create(
        Guid queryId,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        int totalShards,
        IReadOnlyList<int> failedShards,
        long executionMs,
        bool fromCache = false)
    {
        var successfulShards = totalShards - failedShards.Count;
        var degraded = failedShards.Count > 0;
        var reason = degraded
            ? $"{failedShards.Count} of {totalShards} shards failed: [{string.Join(", ", failedShards)}]"
            : null;

        return new(queryId, columns, rows, rows.Count, totalShards,
            successfulShards, failedShards, degraded, reason, executionMs, fromCache);
    }
}
