using DistributedQuery.Coordinator;
using DistributedQuery.Core.Models;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Coordinator;

public sealed class ActiveQueryRegistryTests
{
    [Fact]
    public void BeginQuery_TracksActiveQueryUntilDisposed()
    {
        var registry = new ActiveQueryRegistry();
        var queryId = Guid.NewGuid();

        using (registry.BeginQuery(queryId, ActiveQueryKind.Sync, "abc123", 2))
        {
            registry.ActiveCount.Should().Be(1);
            var page = registry.List(limit: 10, offset: 0);
            page.TotalCount.Should().Be(1);
            page.Queries[0].QueryId.Should().Be(queryId);
            page.Queries[0].Kind.Should().Be(ActiveQueryKind.Sync);
        }

        registry.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Cancel_RequestsCancellationForActiveQuery()
    {
        var registry = new ActiveQueryRegistry();
        var queryId = Guid.NewGuid();

        using var scope = registry.BeginQuery(queryId, ActiveQueryKind.Stream, "def456", 4);

        var result = registry.Cancel(queryId);

        result.Found.Should().BeTrue();
        result.CancellationRequested.Should().BeTrue();
        scope.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Cancel_ReturnsNotFound_WhenQueryIsNotActive()
    {
        var registry = new ActiveQueryRegistry();

        var result = registry.Cancel(Guid.NewGuid());

        result.Found.Should().BeFalse();
        result.CancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void List_AppliesPagination()
    {
        var registry = new ActiveQueryRegistry();
        var scopes = new List<ActiveQueryScope>();

        for (var index = 0; index < 3; index++)
        {
            scopes.Add(registry.BeginQuery(Guid.NewGuid(), ActiveQueryKind.Sync, $"hash-{index}", 1));
        }

        var page = registry.List(limit: 2, offset: 1);

        page.TotalCount.Should().Be(3);
        page.Queries.Should().HaveCount(2);

        foreach (var scope in scopes)
        {
            scope.Dispose();
        }
    }
}
