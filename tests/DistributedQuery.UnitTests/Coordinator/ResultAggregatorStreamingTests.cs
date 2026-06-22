using DistributedQuery.Coordinator;
using DistributedQuery.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DistributedQuery.UnitTests.Coordinator;

public sealed class ResultAggregatorStreamingTests
{
    [Fact]
    public async Task StreamMergeAsync_IncrementalMode_YieldsRowsAsPartialsArrive()
    {
        var aggregator = new ResultAggregator(Substitute.For<ILogger<ResultAggregator>>());
        var queryId = Guid.NewGuid();

        async IAsyncEnumerable<PartialResult> ProducePartials()
        {
            yield return Success(queryId, 0, [["1"], ["2"]], ["id"]);
            await Task.Yield();
            yield return Success(queryId, 1, [["3"]], ["id"]);
        }

        var events = new List<QueryStreamEvent>();
        await foreach (var streamEvent in aggregator.StreamMergeAsync(
                           queryId,
                           ProducePartials(),
                           MergeInstructions.Empty))
        {
            events.Add(streamEvent);
        }

        events.Should().ContainSingle(e => e.Kind == QueryStreamEventKind.Columns);
        events.Where(e => e.Kind == QueryStreamEventKind.Row).Should().HaveCount(3);
        events.Where(e => e.Kind == QueryStreamEventKind.Row)
            .Select(e => e.Row![0])
            .Should()
            .BeEquivalentTo("1", "2", "3");
    }

    [Fact]
    public async Task StreamMergeAsync_OrderedMode_PerformsGlobalMergeBeforeStreamingRows()
    {
        var aggregator = new ResultAggregator(Substitute.For<ILogger<ResultAggregator>>());
        var queryId = Guid.NewGuid();
        var instructions = new MergeInstructions(
            [new OrderByColumn("score", Descending: true)],
            [],
            null,
            null,
            IsDistinct: false);

        async IAsyncEnumerable<PartialResult> ProducePartials()
        {
            yield return Success(queryId, 0, [["100"], ["80"]], ["score"]);
            yield return Success(queryId, 1, [["95"]], ["score"]);
        }

        var rows = new List<IReadOnlyList<string>>();
        await foreach (var streamEvent in aggregator.StreamMergeAsync(
                           queryId,
                           ProducePartials(),
                           instructions))
        {
            if (streamEvent.Kind == QueryStreamEventKind.Row)
            {
                rows.Add(streamEvent.Row!);
            }
        }

        rows.Select(row => row[0]).Should().Equal("100", "95", "80");
    }

    private static PartialResult Success(
        Guid queryId,
        int shardIndex,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<string> columns)
    {
        var descriptors = columns
            .Select(column => new ColumnDescriptor(column, "text", false))
            .ToArray();

        return PartialResult.Success(
            Guid.NewGuid(),
            queryId,
            shardIndex,
            descriptors,
            rows,
            executionMs: 1);
    }
}
