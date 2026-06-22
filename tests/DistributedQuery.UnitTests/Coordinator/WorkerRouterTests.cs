using DistributedQuery.Coordinator;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DistributedQuery.UnitTests.Coordinator;

public class WorkerRouterTests
{
    [Fact]
    public void Route_ThrowsInsufficientNodesException_WhenShardHasNoOwner()
    {
        var router = new WorkerRouter(Substitute.For<ILogger<WorkerRouter>>());
        var queryId = Guid.NewGuid();
        var subQuery = SubQuery.Create(queryId, "SELECT 1", "worker-1", shardIndex: 3, totalShards: 4);
        var healthyNodes = new[]
        {
            new NodeInfo("worker-1", "localhost", 5100, new[] { 0, 1 }, "1.0")
        };

        Action act = () => router.Route(new[] { subQuery }, healthyNodes);

        act.Should().Throw<InsufficientNodesException>();
    }

    [Fact]
    public void Route_RoundRobinsAcrossReplicas_ForSameShardOnSubsequentCalls()
    {
        var router = new WorkerRouter(Substitute.For<ILogger<WorkerRouter>>());
        var queryId = Guid.NewGuid();
        var subQuery = SubQuery.Create(queryId, "SELECT 1", "worker-1", shardIndex: 1, totalShards: 2);
        var healthyNodes = new[]
        {
            new NodeInfo("worker-a", "localhost", 5100, new[] { 1 }, "1.0"),
            new NodeInfo("worker-b", "localhost", 5101, new[] { 1 }, "1.0")
        };

        var first = router.Route(new[] { subQuery }, healthyNodes).Single();
        var second = router.Route(new[] { subQuery }, healthyNodes).Single();
        var third = router.Route(new[] { subQuery }, healthyNodes).Single();

        first.Node.NodeId.Should().Be("worker-a");
        second.Node.NodeId.Should().Be("worker-b");
        third.Node.NodeId.Should().Be("worker-a");
    }
}
