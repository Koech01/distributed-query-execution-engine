using DistributedQuery.Coordinator;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DistributedQuery.UnitTests.Coordinator;

public class QueryPlanningServiceTests
{
    [Fact]
    public async Task PlanAsync_ReturnsCachedPlan_WhenCacheHasEntry()
    {
        var request = QueryRequest.Create("SELECT 1");
        var cachedPlan = QueryPlan.Create("hash", [], MergeInstructions.Empty);
        var cache = Substitute.For<IQueryCache>();
        cache.TryGetPlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(cachedPlan);

        var planner = Substitute.For<IQueryPlanner>();
        var service = new QueryPlanningService(
            cache,
            planner,
            Options.Create(new CoordinatorOptions()),
            Substitute.For<ILogger<QueryPlanningService>>());

        var (plan, fromCache) = await service.PlanAsync(request, CancellationToken.None);

        fromCache.Should().BeTrue();
        plan.Should().Be(cachedPlan);
        await planner.DidNotReceiveWithAnyArgs().PlanAsync(default!, default);
    }
}
