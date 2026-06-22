using DistributedQuery.Coordinator;
using DistributedQuery.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DistributedQuery.UnitTests.Coordinator;

public class ResultAggregatorTests
{
    [Fact]
    public void Merge_OrderByAcrossShards_PerformsGlobalKWayMerge()
    {
        var aggregator = CreateAggregator();
        var queryId = Guid.NewGuid();

        var partials = new[]
        {
            Success(queryId, 0, [["100"], ["80"]], columns: ["score"]),
            Success(queryId, 1, [["95"], ["70"]], columns: ["score"])
        };

        var instructions = new MergeInstructions(
            OrderBy: [new OrderByColumn("score", Descending: true)],
            Aggregates: [],
            Limit: null,
            Offset: null,
            IsDistinct: false);

        var result = aggregator.Merge(queryId, partials, instructions, totalExecutionMs: 15);

        result.Rows.Select(row => row[0]).Should().Equal("100", "95", "80", "70");
    }

    [Fact]
    public void Merge_AggregatesAcrossShards_MergesSumCountAvgMinMax()
    {
        var aggregator = CreateAggregator();
        var queryId = Guid.NewGuid();

        var partials = new[]
        {
            Success(queryId, 0, [["10", "2", "10", "100"]], columns: ["total_sum", "total_count", "min_value", "max_value"]),
            Success(queryId, 1, [["20", "4", "5", "120"]], columns: ["total_sum", "total_count", "min_value", "max_value"])
        };

        var instructions = new MergeInstructions(
            OrderBy: [],
            Aggregates:
            [
                new AggregateOperation(AggregateFunction.Sum, "total_sum", "sum_total"),
                new AggregateOperation(AggregateFunction.Count, "total_count", "count_total"),
                new AggregateOperation(AggregateFunction.Avg, "total_sum", "avg_total"),
                new AggregateOperation(AggregateFunction.Min, "min_value", "min_total"),
                new AggregateOperation(AggregateFunction.Max, "max_value", "max_total")
            ],
            Limit: null,
            Offset: null,
            IsDistinct: false);

        var result = aggregator.Merge(queryId, partials, instructions, totalExecutionMs: 10);
        result.Rows.Should().ContainSingle();
        result.Rows[0].Should().Equal("30", "6", "5", "5", "120");
    }

    [Fact]
    public void Merge_DeduplicatesRows_WhenDistinctIsEnabled()
    {
        var aggregator = CreateAggregator();
        var queryId = Guid.NewGuid();
        var partials = new[]
        {
            Success(queryId, 0, [["1", "alice"], ["2", "bob"]], columns: ["id", "name"]),
            Success(queryId, 1, [["2", "bob"], ["3", "carol"]], columns: ["id", "name"])
        };

        var instructions = new MergeInstructions([], [], null, null, IsDistinct: true);
        var result = aggregator.Merge(queryId, partials, instructions, totalExecutionMs: 5);

        result.Rows.Should().HaveCount(3);
    }

    [Fact]
    public void Merge_AppliesLimitAfterMerge()
    {
        var aggregator = CreateAggregator();
        var queryId = Guid.NewGuid();
        var partials = new[]
        {
            Success(queryId, 0, [["1"], ["2"]], columns: ["id"]),
            Success(queryId, 1, [["3"], ["4"]], columns: ["id"])
        };

        var instructions = new MergeInstructions(
            OrderBy: [new OrderByColumn("id", Descending: false)],
            Aggregates: [],
            Limit: 2,
            Offset: 1,
            IsDistinct: false);

        var result = aggregator.Merge(queryId, partials, instructions, totalExecutionMs: 5);
        result.Rows.Select(row => row[0]).Should().Equal("2", "3");
    }

    private static ResultAggregator CreateAggregator() =>
        new(Substitute.For<ILogger<ResultAggregator>>());

    private static PartialResult Success(
        Guid queryId,
        int shardIndex,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<string> columns)
    {
        var descriptors = columns.Select(name => new ColumnDescriptor(name, "nvarchar", true)).ToArray();
        return PartialResult.Success(
            Guid.NewGuid(),
            queryId,
            shardIndex,
            descriptors,
            rows,
            executionMs: 1);
    }
}
