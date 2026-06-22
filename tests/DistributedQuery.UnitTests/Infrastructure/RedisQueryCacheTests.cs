using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Caching;
using FluentAssertions;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace DistributedQuery.UnitTests.Infrastructure;

public class RedisQueryCacheTests
{
    private static readonly MessagePackSerializerOptions SerializerOptions =
        MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance);

    private static (RedisQueryCache cache, IDatabase db) BuildCache(bool enableCompression = false)
    {
        var db         = Substitute.For<IDatabase>();
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase().Returns(db);

        var options = Options.Create(new RedisOptions
        {
            InstanceName      = "test:",
            EnableCompression = enableCompression
        });

        var cache = new RedisQueryCache(multiplexer, options, NullLogger<RedisQueryCache>.Instance);
        return (cache, db);
    }

    private static QueryPlan MakePlan()
    {
        var sub = SubQuery.Create(Guid.NewGuid(), "SELECT 1", "node-01", 0, 1);
        return QueryPlan.Create("testhash", [sub], MergeInstructions.Empty);
    }

    private static QueryResult MakeResult(Guid queryId) =>
        QueryResult.Create(queryId, ["id"], [["1"]], 1, [], 10);

    [Fact]
    public async Task SetPlanAsync_UsesCompressionWhenPayloadExceedsThreshold()
    {
        var (cache, db) = BuildCache(enableCompression: true);
        var options = Options.Create(new RedisOptions
        {
            InstanceName = "test:",
            EnableCompression = true,
            CompressionThresholdBytes = 1
        });
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase().Returns(db);
        cache = new RedisQueryCache(multiplexer, options, NullLogger<RedisQueryCache>.Instance);

        RedisValue captured = RedisValue.Null;
        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Do<RedisValue>(v => captured = v), Arg.Any<TimeSpan?>())
          .Returns(true);

        await cache.SetPlanAsync("plan::abc", MakePlan(), TimeSpan.FromHours(1));

        captured.HasValue.Should().BeTrue();
    }

    // TryGetPlanAsync

    [Fact]
    public async Task TryGetPlanAsync_CacheMiss_ReturnsNull()
    {
        var (cache, db) = BuildCache();
        db.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);

        var result = await cache.TryGetPlanAsync("plan::abc");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetPlanAsync_CacheHit_DeserializesPlan()
    {
        var (cache, db) = BuildCache();
        var plan  = MakePlan();
        var bytes = MessagePackSerializer.Serialize(plan, SerializerOptions);
        db.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)bytes);

        var result = await cache.TryGetPlanAsync("plan::abc");

        result.Should().NotBeNull();
        result!.PlanHash.Should().Be(plan.PlanHash);
        result.SubQueries.Should().HaveCount(1);
    }

    [Fact]
    public async Task TryGetPlanAsync_PrefixesKeyWithInstanceName()
    {
        var (cache, db) = BuildCache();
        db.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);

        await cache.TryGetPlanAsync("plan::myhash");

        await db.Received(1).StringGetAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "test:plan::myhash"));
    }

    [Fact]
    public async Task TryGetPlanAsync_RedisThrows_ReturnsNullGracefully()
    {
        var (cache, db) = BuildCache();
        db.StringGetAsync(Arg.Any<RedisKey>()).ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var result = await cache.TryGetPlanAsync("plan::abc");

        result.Should().BeNull();
    }

    // SetPlanAsync

    [Fact]
    public async Task SetPlanAsync_CallsStringSetWithCorrectKeyAndTtl()
    {
        var (cache, db) = BuildCache();
        var plan = MakePlan();
        var ttl  = TimeSpan.FromHours(1);

        await cache.SetPlanAsync("plan::abc", plan, ttl);

        await db.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "test:plan::abc"),
            Arg.Any<RedisValue>(),
            Arg.Is<TimeSpan?>(t => t == ttl));
    }

    [Fact]
    public async Task SetPlanAsync_RedisThrows_DoesNotPropagateException()
    {
        var (cache, db) = BuildCache();
        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>())
          .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var act = () => cache.SetPlanAsync("plan::abc", MakePlan(), TimeSpan.FromHours(1));

        await act.Should().NotThrowAsync();
    }

    // TryGetResultAsync

    [Fact]
    public async Task TryGetResultAsync_CacheMiss_ReturnsNull()
    {
        var (cache, db) = BuildCache();
        db.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);

        var result = await cache.TryGetResultAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetResultAsync_CacheHit_DeserializesResult()
    {
        var (cache, db) = BuildCache();
        var queryId = Guid.NewGuid();
        var expected = MakeResult(queryId);
        var bytes    = MessagePackSerializer.Serialize(expected, SerializerOptions);
        db.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)bytes);

        var result = await cache.TryGetResultAsync(queryId);

        result.Should().NotBeNull();
        result!.QueryId.Should().Be(queryId);
        result.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task TryGetResultAsync_UsesResultPrefixedKey()
    {
        var (cache, db) = BuildCache();
        var queryId = Guid.NewGuid();
        db.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);

        await cache.TryGetResultAsync(queryId);

        var expectedKey = "test:" + CacheKeyBuilder.ForResult(queryId);
        await db.Received(1).StringGetAsync(
            Arg.Is<RedisKey>(k => k.ToString() == expectedKey));
    }

    [Fact]
    public async Task TryGetResultAsync_RedisThrows_ReturnsNullGracefully()
    {
        var (cache, db) = BuildCache();
        db.StringGetAsync(Arg.Any<RedisKey>()).ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var result = await cache.TryGetResultAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // SetResultAsync

    [Fact]
    public async Task SetResultAsync_CallsStringSetWithCorrectKeyAndTtl()
    {
        var (cache, db) = BuildCache();
        var queryId = Guid.NewGuid();
        var result  = MakeResult(queryId);
        var ttl     = TimeSpan.FromMinutes(5);

        await cache.SetResultAsync(queryId, result, ttl);

        var expectedKey = "test:" + CacheKeyBuilder.ForResult(queryId);
        await db.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString() == expectedKey),
            Arg.Any<RedisValue>(),
            Arg.Is<TimeSpan?>(t => t == ttl));
    }

    [Fact]
    public async Task SetResultAsync_RedisThrows_DoesNotPropagateException()
    {
        var (cache, db) = BuildCache();
        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>())
          .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var act = () => cache.SetResultAsync(Guid.NewGuid(), MakeResult(Guid.NewGuid()), TimeSpan.FromMinutes(5));

        await act.Should().NotThrowAsync();
    }

    // Cancellation

    [Fact]
    public async Task TryGetPlanAsync_CancelledToken_ThrowsOperationCancelled()
    {
        var (cache, db) = BuildCache();
        var tcs = new TaskCompletionSource<RedisValue>();
        db.StringGetAsync(Arg.Any<RedisKey>()).Returns(tcs.Task);

        var cts = new CancellationTokenSource();
        var task = cache.TryGetPlanAsync("plan::abc", cts.Token);
        await cts.CancelAsync();

        await task.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
    }
}
