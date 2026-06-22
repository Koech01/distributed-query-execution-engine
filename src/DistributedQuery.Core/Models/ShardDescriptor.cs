namespace DistributedQuery.Core.Models;

public record ShardDescriptor(
    int ShardIndex,
    int TotalShards,
    string ShardKey,
    string TableName
);
