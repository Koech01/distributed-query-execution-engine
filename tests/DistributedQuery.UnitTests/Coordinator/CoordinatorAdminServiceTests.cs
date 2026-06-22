using DistributedQuery.Coordinator;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Grpc;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Coordinator;

public sealed class CoordinatorAdminServiceTests
{
    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsActiveQueryAndWorkerCounts()
    {
        var registry = new ActiveQueryRegistry();
        using (registry.BeginQuery(Guid.NewGuid(), ActiveQueryKind.Sync, "abc", 1))
        {
            var service = CreateService(registry, nodes: 2, planEntries: 5, resultEntries: 3, statusEntries: 1);
            var stats = await service.GetDashboardStatsAsync();

            stats.ActiveQueries.Should().Be(1);
            stats.HealthyWorkers.Should().Be(2);
            stats.PlanCacheEntries.Should().Be(5);
            stats.ResultCacheEntries.Should().Be(3);
            stats.AsyncQueryStatusEntries.Should().Be(1);
        }
    }

    [Fact]
    public async Task CancelQueryAsync_DelegatesToRegistry()
    {
        var registry = new ActiveQueryRegistry();
        var queryId = Guid.NewGuid();
        using var scope = registry.BeginQuery(queryId, ActiveQueryKind.Async, "hash", 3);

        var service = CreateService(registry);
        var result = await service.CancelQueryAsync(queryId);

        result.Found.Should().BeTrue();
        scope.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    private static CoordinatorAdminService CreateService(
        ActiveQueryRegistry registry,
        int nodes = 1,
        long planEntries = 0,
        long resultEntries = 0,
        long statusEntries = 0)
    {
        var nodeRegistry = Substitute.For<INodeRegistry>();
        nodeRegistry.GetHealthyNodesAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(0, nodes)
                .Select(index => new NodeInfo($"worker-{index}", "127.0.0.1", 5100 + index, [index], "1.0.0"))
                .ToArray());

        var queryCacheAdmin = Substitute.For<IQueryCacheAdmin>();
        queryCacheAdmin.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminCacheStats(planEntries, resultEntries, statusEntries, DateTimeOffset.UtcNow));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(nameof(WorkerHealthProbe))
            .Returns(new HttpClient(new HttpClientHandler()) { Timeout = TimeSpan.FromMilliseconds(100) });

        var workerHealthProbe = new WorkerHealthProbe(
            httpClientFactory,
            NullLogger<WorkerHealthProbe>.Instance);

        return new CoordinatorAdminService(
            registry,
            nodeRegistry,
            queryCacheAdmin,
            workerHealthProbe,
            NullLogger<CoordinatorAdminService>.Instance);
    }
}
