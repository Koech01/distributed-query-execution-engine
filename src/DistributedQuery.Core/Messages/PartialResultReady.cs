namespace DistributedQuery.Core.Messages;

public record PartialResultReady(
    Guid SubQueryId,
    Guid ParentQueryId,
    int ShardIndex,
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    long ExecutionMs,
    string? TraceParent = null,
    string? TraceState = null,
    int TotalShards = 0,
    int SchemaVersion = 1
);
