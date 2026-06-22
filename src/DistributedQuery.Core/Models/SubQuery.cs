namespace DistributedQuery.Core.Models;

public record SubQuery(
    Guid SubQueryId,
    Guid ParentQueryId,
    string Sql,
    string TargetNodeId,
    int ShardIndex,
    int TotalShards,
    IReadOnlyList<QueryParameter> Parameters,
    int TimeoutMs = 0
)
{
    public static SubQuery Create(
        Guid parentId,
        string sql,
        string nodeId,
        int shardIndex,
        int totalShards,
        IReadOnlyList<QueryParameter>? parameters = null,
        int timeoutMs = 0) =>
        new(Guid.NewGuid(), parentId, sql, nodeId, shardIndex, totalShards, parameters ?? [], timeoutMs);
}
