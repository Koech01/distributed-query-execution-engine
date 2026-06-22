using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Messages;

public record SubQueryDispatched(
    Guid SubQueryId,
    Guid ParentQueryId,
    string Sql,
    int ShardIndex,
    int TotalShards,
    IReadOnlyList<QueryParameter> Parameters,
    int TimeoutMs,
    string? TraceParent,
    string? TraceState,
    int SchemaVersion = 1
);
