using DistributedQuery.Core.Models;

namespace DistributedQuery.Coordinator;

public static class QueryPlanMapper
{
    public static QueryPlanDetails ToDetails(QueryPlan plan, bool fromCache)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var clusterShardCount = plan.SubQueries.Count > 0
            ? plan.SubQueries[0].TotalShards
            : 0;

        var subQueries = plan.SubQueries
            .Select(subQuery => new QueryPlanSubQueryDetails(
                subQuery.SubQueryId,
                subQuery.ShardIndex,
                subQuery.TotalShards,
                subQuery.Sql))
            .ToArray();

        var merge = new QueryPlanMergeDetails(
            plan.MergeInstructions.OrderBy,
            plan.MergeInstructions.Aggregates,
            plan.MergeInstructions.Limit,
            plan.MergeInstructions.Offset,
            plan.MergeInstructions.IsDistinct);

        return new QueryPlanDetails(
            plan.PlanId,
            plan.PlanHash,
            fromCache,
            ResolveTargetingStrategy(plan.SubQueries),
            clusterShardCount,
            subQueries,
            merge,
            plan.CreatedAt);
    }

    public static QueryStreamMode ResolveStreamMode(MergeInstructions instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);

        if (instructions.Aggregates.Count > 0 ||
            instructions.IsDistinct ||
            instructions.Limit is not null ||
            instructions.Offset is not null)
        {
            return QueryStreamMode.Buffered;
        }

        if (instructions.OrderBy.Count > 0)
        {
            return QueryStreamMode.Ordered;
        }

        return QueryStreamMode.Incremental;
    }

    private static string ResolveTargetingStrategy(IReadOnlyList<SubQuery> subQueries)
    {
        if (subQueries.Count == 0)
        {
            return "none";
        }

        if (subQueries.Count == 1)
        {
            return "single_shard";
        }

        var clusterShardCount = subQueries[0].TotalShards;
        return subQueries.Count >= clusterShardCount ? "broadcast" : "shard_range";
    }
}
