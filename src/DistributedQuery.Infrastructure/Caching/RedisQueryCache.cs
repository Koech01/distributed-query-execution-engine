using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DistributedQuery.Infrastructure.Caching;

/// <summary>
/// Implements IQueryCache using Redis with MessagePack binary serialization.
/// All operations degrade gracefully on Redis failure - a cache miss is returned
/// rather than propagating infrastructure exceptions to callers.
/// </summary>
public sealed class RedisQueryCache : IQueryCache, IQueryCacheAdmin
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;
    private readonly MessagePackSerializerOptions _compressedSerializerOptions;
    private readonly MessagePackSerializerOptions _uncompressedSerializerOptions;
    private readonly ILogger<RedisQueryCache> _logger;

    public RedisQueryCache(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisQueryCache> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;

        _uncompressedSerializerOptions = MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance);

        _compressedSerializerOptions = _uncompressedSerializerOptions
            .WithCompression(MessagePackCompression.Lz4BlockArray);
    }

    public async Task<QueryPlan?> TryGetPlanAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var key = _options.InstanceName + cacheKey;
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key).WaitAsync(cancellationToken);

            if (!value.HasValue)
            {
                _logger.LogDebug("Plan cache miss for key {CacheKey}", cacheKey);
                return null;
            }

            var bytes = (byte[])value!;
            var plan = Deserialize<QueryPlan>(bytes);
            _logger.LogDebug("Plan cache hit for key {CacheKey}", cacheKey);
            return plan;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis plan cache read failed for key {CacheKey} - treating as miss", cacheKey);
            return null;
        }
    }

    public async Task SetPlanAsync(
        string cacheKey,
        QueryPlan plan,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var key = _options.InstanceName + cacheKey;
        try
        {
            var bytes = Serialize(plan);
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, bytes, ttl).WaitAsync(cancellationToken);
            _logger.LogDebug("Plan cached with key {CacheKey}, TTL {TtlSeconds}s, size {Bytes}B",
                cacheKey, (int)ttl.TotalSeconds, bytes.Length);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis plan cache write failed for key {CacheKey} - continuing without cache", cacheKey);
        }
    }

    public async Task<QueryResult?> TryGetResultAsync(Guid queryId, CancellationToken cancellationToken = default)
    {
        var key = _options.InstanceName + CacheKeyBuilder.ForResult(queryId);
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key).WaitAsync(cancellationToken);

            if (!value.HasValue)
            {
                _logger.LogDebug("Result cache miss for query {QueryId}", queryId);
                return null;
            }

            var bytes = (byte[])value!;
            var result = Deserialize<QueryResult>(bytes);
            _logger.LogDebug("Result cache hit for query {QueryId}", queryId);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis result cache read failed for query {QueryId} - treating as miss", queryId);
            return null;
        }
    }

    public async Task SetResultAsync(
        Guid queryId,
        QueryResult result,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var key = _options.InstanceName + CacheKeyBuilder.ForResult(queryId);
        try
        {
            var bytes = Serialize(result);
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, bytes, ttl).WaitAsync(cancellationToken);
            _logger.LogDebug("Result cached for query {QueryId}, TTL {TtlSeconds}s, size {Bytes}B",
                queryId, (int)ttl.TotalSeconds, bytes.Length);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis result cache write failed for query {QueryId} - continuing without cache", queryId);
        }
    }

    public async Task<AdminCacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var planEntries = await CountKeysAsync($"{_options.InstanceName}plan::*", cancellationToken)
                .ConfigureAwait(false);
            var resultEntries = await CountKeysAsync($"{_options.InstanceName}result::*", cancellationToken)
                .ConfigureAwait(false);
            var statusEntries = await CountKeysAsync($"{_options.InstanceName}query::*::status", cancellationToken)
                .ConfigureAwait(false);

            return new AdminCacheStats(planEntries, resultEntries, statusEntries, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache stats collection failed");
            return new AdminCacheStats(0, 0, 0, DateTimeOffset.UtcNow);
        }
    }

    public async Task<AdminCacheFlushResult> FlushPlansAsync(
        string? planHash = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(planHash) &&
            !System.Text.RegularExpressions.Regex.IsMatch(planHash, "^[a-f0-9]{64}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            throw new ArgumentException("Plan hash must be a 64-character hexadecimal SHA256 value.", nameof(planHash));
        }

        var pattern = string.IsNullOrWhiteSpace(planHash)
            ? $"{_options.InstanceName}plan::*"
            : $"{_options.InstanceName}plan::{planHash}";

        try
        {
            var deleted = await DeleteKeysAsync(pattern, cancellationToken).ConfigureAwait(false);
            var scope = string.IsNullOrWhiteSpace(planHash) ? "all_plans" : $"plan_hash:{planHash}";

            _logger.LogInformation(
                "Flushed {DeletedCount} plan cache entries with scope {Scope}",
                deleted,
                scope);

            return new AdminCacheFlushResult(deleted, scope, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis plan cache flush failed for scope {Scope}", planHash ?? "all");
            throw;
        }
    }

    private async Task<long> CountKeysAsync(string pattern, CancellationToken cancellationToken)
    {
        long count = 0;
        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            if (!server.IsConnected)
            {
                continue;
            }

            await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(cancellationToken))
            {
                _ = key;
                count++;
            }
        }

        return count;
    }

    private async Task<long> DeleteKeysAsync(string pattern, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        long deleted = 0;

        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            if (!server.IsConnected)
            {
                continue;
            }

            var batch = new List<RedisKey>();
            await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(cancellationToken))
            {
                batch.Add(key);
                if (batch.Count < 250)
                {
                    continue;
                }

                deleted += await db.KeyDeleteAsync(batch.ToArray()).ConfigureAwait(false);
                batch.Clear();
            }

            if (batch.Count > 0)
            {
                deleted += await db.KeyDeleteAsync(batch.ToArray()).ConfigureAwait(false);
            }
        }

        return deleted;
    }

    private byte[] Serialize<T>(T value)
    {
        var options = ShouldCompress(value)
            ? _compressedSerializerOptions
            : _uncompressedSerializerOptions;

        return MessagePackSerializer.Serialize(value, options);
    }

    private T Deserialize<T>(byte[] bytes)
    {
        try
        {
            return MessagePackSerializer.Deserialize<T>(bytes, _compressedSerializerOptions);
        }
        catch (MessagePackSerializationException)
        {
            return MessagePackSerializer.Deserialize<T>(bytes, _uncompressedSerializerOptions);
        }
    }

    private bool ShouldCompress<T>(T value)
    {
        if (!_options.EnableCompression)
        {
            return false;
        }

        var bytes = MessagePackSerializer.Serialize(value, _uncompressedSerializerOptions);
        return bytes.Length >= _options.CompressionThresholdBytes;
    }
}
