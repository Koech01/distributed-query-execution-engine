using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DistributedQuery.IntegrationTests.Infrastructure;

public sealed class RedisQueryCacheIntegrationTests
{
    private readonly bool _redisAvailable;
    private readonly RedisQueryCache? _cache;

    public RedisQueryCacheIntegrationTests()
    {
        try
        {
            var multiplexer = ConnectionMultiplexer.Connect(
                "localhost:6379,abortConnect=false,connectTimeout=1000,connectRetry=1");

            if (!multiplexer.IsConnected)
            {
                _redisAvailable = false;
                return;
            }

            _cache = new RedisQueryCache(
                multiplexer,
                Options.Create(new RedisOptions
                {
                    InstanceName = $"it:{Guid.NewGuid():N}:",
                    EnableCompression = true,
                    CompressionThresholdBytes = 1
                }),
                NullLogger<RedisQueryCache>.Instance);
            _redisAvailable = true;
        }
        catch (RedisConnectionException)
        {
            _redisAvailable = false;
        }
    }

    [Fact]
    public async Task SetPlan_ThenGetPlan_ReturnsEquivalentPlan()
    {
        if (!_redisAvailable)
        {
            return;
        }

        var sub = SubQuery.Create(Guid.NewGuid(), "SELECT 1", "node-01", 0, 4);
        var plan = QueryPlan.Create("integration-hash", [sub], MergeInstructions.Empty);
        var cacheKey = CacheKeyBuilder.ForPlan("SELECT 1", []);

        await _cache!.SetPlanAsync(cacheKey, plan, TimeSpan.FromMinutes(5));
        var retrieved = await _cache.TryGetPlanAsync(cacheKey);

        retrieved.Should().NotBeNull();
        retrieved!.PlanHash.Should().Be(plan.PlanHash);
        retrieved.SubQueries.Should().HaveCount(1);
    }

    [Fact]
    public async Task SetResult_ThenGetResult_ReturnsEquivalentResult()
    {
        if (!_redisAvailable)
        {
            return;
        }

        var queryId = Guid.NewGuid();
        var expected = QueryResult.Create(queryId, ["id"], [["42"]], 1, [], 12);

        await _cache!.SetResultAsync(queryId, expected, TimeSpan.FromMinutes(5));
        var retrieved = await _cache.TryGetResultAsync(queryId);

        retrieved.Should().NotBeNull();
        retrieved!.QueryId.Should().Be(queryId);
        retrieved.Rows.Should().ContainSingle().Which.Should().ContainSingle().Which.Should().Be("42");
    }
}
