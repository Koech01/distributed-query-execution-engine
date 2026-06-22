namespace DistributedQuery.Core.Models;

public record QueryPlanDetails(
    Guid PlanId,
    string PlanHash,
    bool FromCache,
    string TargetingStrategy,
    int ClusterShardCount,
    IReadOnlyList<QueryPlanSubQueryDetails> SubQueries,
    QueryPlanMergeDetails MergeInstructions,
    DateTimeOffset CreatedAt);

public record QueryPlanSubQueryDetails(
    Guid SubQueryId,
    int ShardIndex,
    int TotalShards,
    string Sql);

public record QueryPlanMergeDetails(
    IReadOnlyList<OrderByColumn> OrderBy,
    IReadOnlyList<AggregateOperation> Aggregates,
    int? Limit,
    int? Offset,
    bool IsDistinct);
