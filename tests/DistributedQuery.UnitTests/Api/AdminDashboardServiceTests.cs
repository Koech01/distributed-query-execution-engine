using DistributedQuery.Api.Services;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Api;

public sealed class AdminDashboardServiceTests
{
    [Fact]
    public async Task GetStatsAsync_CachesResultsForThirtySeconds()
    {
        var coordinatorClient = Substitute.For<IQueryCoordinatorClient>();
        var queryCacheAdmin = Substitute.For<IQueryCacheAdmin>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new AdminDashboardService(
            coordinatorClient,
            queryCacheAdmin,
            memoryCache,
            NullLogger<AdminDashboardService>.Instance);

        coordinatorClient.GetAdminDashboardAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminDashboardStats(1, 2, 2, 0, 0, 0, DateTimeOffset.UtcNow));
        queryCacheAdmin.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminCacheStats(8, 4, 2, DateTimeOffset.UtcNow));

        var first = await service.GetStatsAsync();
        var second = await service.GetStatsAsync();

        first.PlanCacheEntries.Should().Be(8);
        second.PlanCacheEntries.Should().Be(8);
        await coordinatorClient.Received(1).GetAdminDashboardAsync(Arg.Any<CancellationToken>());
        await queryCacheAdmin.Received(1).GetStatsAsync(Arg.Any<CancellationToken>());
    }
}
