using DistributedQuery.Core.Exceptions;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Core;

public class ExceptionTests
{
    // QueryParseException

    [Fact]
    public void QueryParseException_StoresSqlHashAndErrors()
    {
        var errors = new[] { "Unexpected token at line 1", "Missing FROM clause" };
        var ex = new QueryParseException("Parse failed", "deadbeef", errors);

        ex.SqlHash.Should().Be("deadbeef");
        ex.ParseErrors.Should().BeEquivalentTo(errors);
        ex.Message.Should().Be("Parse failed");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void QueryParseException_WithInnerException_StoresInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new QueryParseException("Parse failed", "abc", ["error"], inner);

        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void QueryParseException_EmptyErrors_IsAllowed()
    {
        var ex = new QueryParseException("Parse failed", "hash", []);

        ex.ParseErrors.Should().BeEmpty();
    }

    // QueryTimeoutException

    [Fact]
    public void QueryTimeoutException_StoresQueryIdAndTimeout()
    {
        var queryId = Guid.NewGuid();
        var timeout = TimeSpan.FromSeconds(30);
        var ex = new QueryTimeoutException(queryId, timeout);

        ex.QueryId.Should().Be(queryId);
        ex.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void QueryTimeoutException_MessageContainsMilliseconds()
    {
        var ex = new QueryTimeoutException(Guid.NewGuid(), TimeSpan.FromSeconds(30));

        ex.Message.Should().Contain("30000ms");
    }

    [Fact]
    public void QueryTimeoutException_MessageContainsQueryId()
    {
        var queryId = Guid.NewGuid();
        var ex = new QueryTimeoutException(queryId, TimeSpan.FromSeconds(10));

        ex.Message.Should().Contain(queryId.ToString());
    }

    // InsufficientNodesException

    [Fact]
    public void InsufficientNodesException_StoresRequiredAndAvailable()
    {
        var ex = new InsufficientNodesException(requiredShards: 8, availableNodes: 3);

        ex.RequiredShards.Should().Be(8);
        ex.AvailableNodes.Should().Be(3);
    }

    [Fact]
    public void InsufficientNodesException_MessageContainsBothCounts()
    {
        var ex = new InsufficientNodesException(8, 3);

        ex.Message.Should().Contain("8").And.Contain("3");
    }

    [Fact]
    public void InsufficientNodesException_ZeroAvailableNodes_IsValid()
    {
        var ex = new InsufficientNodesException(4, 0);

        ex.AvailableNodes.Should().Be(0);
        ex.Message.Should().Contain("0");
    }

    [Fact]
    public void ShardConfigurationException_StoresTableName()
    {
        var ex = new ShardConfigurationException("orders");

        ex.TableName.Should().Be("orders");
        ex.Message.Should().Contain("orders");
    }

    // ShardExecutionException

    [Fact]
    public void ShardExecutionException_StoresShardIndexAndSubQueryId()
    {
        var subQueryId = Guid.NewGuid();
        var ex = new ShardExecutionException(3, subQueryId, "DB connection failed");

        ex.ShardIndex.Should().Be(3);
        ex.SubQueryId.Should().Be(subQueryId);
        ex.Message.Should().Be("DB connection failed");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void ShardExecutionException_WithInnerException_StoresInner()
    {
        var inner = new TimeoutException("query timed out");
        var ex = new ShardExecutionException(1, Guid.NewGuid(), "Shard failed", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void ShardExecutionException_ShardIndexZero_IsValid()
    {
        var ex = new ShardExecutionException(0, Guid.NewGuid(), "error");

        ex.ShardIndex.Should().Be(0);
    }
}
