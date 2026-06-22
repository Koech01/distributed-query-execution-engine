using DistributedQuery.Coordinator;
using DistributedQuery.Core.Models;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Coordinator;

public sealed class QueryPlanMapperTests
{
    [Fact]
    public void ToDetails_BroadcastQuery_UsesBroadcastTargetingStrategy()
    {
        var parentId = Guid.NewGuid();
        var subQueries = new[]
        {
            SubQuery.Create(parentId, "SELECT id FROM orders WHERE shard = 0", "node-0", 0, 2),
            SubQuery.Create(parentId, "SELECT id FROM orders WHERE shard = 1", "node-1", 1, 2)
        };
        var plan = QueryPlan.Create("hash", subQueries, MergeInstructions.Empty);

        var details = QueryPlanMapper.ToDetails(plan, fromCache: false);

        details.TargetingStrategy.Should().Be("broadcast");
        details.ClusterShardCount.Should().Be(2);
        details.SubQueries.Should().HaveCount(2);
        details.FromCache.Should().BeFalse();
    }

    [Fact]
    public void ResolveStreamMode_OrderByOnly_ReturnsOrdered()
    {
        var instructions = new MergeInstructions(
            [new OrderByColumn("id", Descending: false)],
            [],
            null,
            null,
            IsDistinct: false);

        QueryPlanMapper.ResolveStreamMode(instructions).Should().Be(QueryStreamMode.Ordered);
    }

    [Fact]
    public void ResolveStreamMode_Aggregates_ReturnsBuffered()
    {
        var instructions = new MergeInstructions(
            [],
            [new AggregateOperation(AggregateFunction.Sum, "amount", "total")],
            null,
            null,
            IsDistinct: false);

        QueryPlanMapper.ResolveStreamMode(instructions).Should().Be(QueryStreamMode.Buffered);
    }
}
