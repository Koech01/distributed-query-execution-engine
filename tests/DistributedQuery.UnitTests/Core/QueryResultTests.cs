using DistributedQuery.Core.Models;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Core;

public class QueryResultTests
{
    private static readonly IReadOnlyList<string> _columns = ["id", "name", "amount"];
    private static readonly IReadOnlyList<IReadOnlyList<string>> _rows = [["1", "Alice", "100"], ["2", "Bob", "200"]];

    [Fact]
    public void Create_NotDegraded_WhenNoFailedShards()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 4, [], 50);

        result.Degraded.Should().BeFalse();
        result.DegradationReason.Should().BeNull();
        result.SuccessfulShards.Should().Be(4);
        result.FailedShards.Should().BeEmpty();
    }

    [Fact]
    public void Create_Degraded_WhenOneShardFailed()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 4, [2], 80);

        result.Degraded.Should().BeTrue();
        result.SuccessfulShards.Should().Be(3);
        result.FailedShards.Should().ContainSingle().Which.Should().Be(2);
        result.DegradationReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_Degraded_WhenMultipleShardsFailed()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 8, [1, 3, 5], 120);

        result.Degraded.Should().BeTrue();
        result.SuccessfulShards.Should().Be(5);
        result.FailedShards.Should().BeEquivalentTo([1, 3, 5]);
        result.DegradationReason.Should().Contain("3 of 8");
    }

    [Fact]
    public void Create_DegradationReason_ListsFailedShardIndices()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 4, [0, 2], 60);

        result.DegradationReason.Should().Contain("0").And.Contain("2");
    }

    [Fact]
    public void Create_RowCountMatchesRowsLength()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 4, [], 50);

        result.RowCount.Should().Be(_rows.Count);
    }

    [Fact]
    public void Create_StoresQueryId()
    {
        var queryId = Guid.NewGuid();
        var result = QueryResult.Create(queryId, _columns, _rows, 4, [], 50);

        result.QueryId.Should().Be(queryId);
    }

    [Fact]
    public void Create_StoresExecutionMs()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 4, [], 87);

        result.ExecutionMs.Should().Be(87);
    }

    [Fact]
    public void Create_FromCacheFalseByDefault()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 4, [], 50);

        result.FromCache.Should().BeFalse();
    }

    [Fact]
    public void Create_FromCacheTrueWhenSpecified()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 4, [], 2, fromCache: true);

        result.FromCache.Should().BeTrue();
    }

    [Fact]
    public void Create_StoresColumns()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, _rows, 4, [], 50);

        result.Columns.Should().BeEquivalentTo(_columns);
    }

    [Fact]
    public void Create_AllShardsFailed_DegradedWithZeroSuccessful()
    {
        var result = QueryResult.Create(Guid.NewGuid(), _columns, [], 4, [0, 1, 2, 3], 30);

        result.Degraded.Should().BeTrue();
        result.SuccessfulShards.Should().Be(0);
        result.FailedShards.Should().HaveCount(4);
    }
}
