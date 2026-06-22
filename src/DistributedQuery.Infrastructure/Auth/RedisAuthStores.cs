using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Caching;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DistributedQuery.Infrastructure.Auth;

public sealed class RedisOAuthStateStore : IOAuthStateStore
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Auth.OAuthStateStore");

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisOptions _redisOptions;

    public RedisOAuthStateStore(IConnectionMultiplexer connectionMultiplexer, IOptions<RedisOptions> redisOptions)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _redisOptions = redisOptions.Value;
    }

    public async Task StoreAuthorizationRequestAsync(
        OAuthAuthorizationRequest request,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisOAuthStateStore.Store", ActivityKind.Client);
        activity?.SetTag("auth.provider", request.Provider.ToString());

        var payload = JsonSerializer.Serialize(request);
        await GetDatabase()
            .StringSetAsync(StateKey(request.Provider, request.State), payload, ttl)
            .ConfigureAwait(false);
    }

    public async Task<OAuthAuthorizationRequest?> ConsumeAuthorizationRequestAsync(
        AuthProviderKind provider,
        string state,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisOAuthStateStore.Consume", ActivityKind.Client);
        activity?.SetTag("auth.provider", provider.ToString());

        var key = StateKey(provider, state);
        var payload = await GetDatabase().StringGetAsync(key).ConfigureAwait(false);
        if (payload.IsNullOrEmpty)
        {
            return null;
        }

        await GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
        return JsonSerializer.Deserialize<OAuthAuthorizationRequest>(payload!);
    }

    private IDatabase GetDatabase() => _connectionMultiplexer.GetDatabase();

    private string StateKey(AuthProviderKind provider, string state) =>
        $"{_redisOptions.InstanceName}auth:oauth:state:{provider.ToString().ToLowerInvariant()}:{state}";
}

public sealed class RedisAuthExchangeCodeStore : IAuthExchangeCodeStore
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Auth.ExchangeCodeStore");

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisOptions _redisOptions;

    public RedisAuthExchangeCodeStore(IConnectionMultiplexer connectionMultiplexer, IOptions<RedisOptions> redisOptions)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _redisOptions = redisOptions.Value;
    }

    public async Task<string> CreateExchangeCodeAsync(string userId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisAuthExchangeCodeStore.Create", ActivityKind.Client);
        activity?.SetTag("auth.user_id", userId);

        var exchangeCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await GetDatabase()
            .StringSetAsync(ExchangeKey(exchangeCode), userId, ttl)
            .ConfigureAwait(false);

        return exchangeCode;
    }

    public async Task<string?> ConsumeExchangeCodeAsync(string exchangeCode, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisAuthExchangeCodeStore.Consume", ActivityKind.Client);

        var key = ExchangeKey(exchangeCode);
        var userId = await GetDatabase().StringGetAsync(key).ConfigureAwait(false);
        if (userId.IsNullOrEmpty)
        {
            return null;
        }

        await GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
        return userId;
    }

    private IDatabase GetDatabase() => _connectionMultiplexer.GetDatabase();

    private string ExchangeKey(string exchangeCode) =>
        $"{_redisOptions.InstanceName}auth:exchange:{exchangeCode}";
}
