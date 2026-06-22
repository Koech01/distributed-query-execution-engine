using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DistributedQuery.Infrastructure.Auth;

public sealed class RedisUserRepository : IUserRepository
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Auth.UserRepository");

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<RedisUserRepository> _logger;

    public RedisUserRepository(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisOptions> redisOptions,
        ILogger<RedisUserRepository> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _redisOptions = redisOptions.Value;
        _logger = logger;
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisUserRepository.FindByEmail", ActivityKind.Client);
        activity?.SetTag("auth.lookup", "email");

        var normalizedEmail = NormalizeEmail(email);
        var userId = await GetDatabase().StringGetAsync(EmailKey(normalizedEmail)).ConfigureAwait(false);
        if (userId.IsNullOrEmpty)
        {
            return null;
        }

        return await FindByIdAsync(userId!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserAccount?> FindByExternalLoginAsync(
        AuthProviderKind provider,
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisUserRepository.FindByExternalLogin", ActivityKind.Client);
        activity?.SetTag("auth.provider", provider.ToString());

        var userId = await GetDatabase()
            .StringGetAsync(ExternalLoginKey(provider, providerKey))
            .ConfigureAwait(false);

        if (userId.IsNullOrEmpty)
        {
            return null;
        }

        return await FindByIdAsync(userId!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserAccount?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisUserRepository.FindById", ActivityKind.Client);

        var payload = await GetDatabase().StringGetAsync(UserKey(userId)).ConfigureAwait(false);
        if (payload.IsNullOrEmpty)
        {
            return null;
        }

        return DeserializeUser(payload!);
    }

    public async Task CreateAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisUserRepository.Create", ActivityKind.Client);
        activity?.SetTag("auth.user_id", user.UserId);

        var database = GetDatabase();
        var transaction = database.CreateTransaction();

        _ = transaction.StringSetAsync(UserKey(user.UserId), SerializeUser(user));
        _ = transaction.StringSetAsync(EmailKey(user.Email), user.UserId);

        foreach (var login in user.ExternalLogins)
        {
            if (Enum.TryParse<AuthProviderKind>(login.Provider, true, out var provider))
            {
                _ = transaction.StringSetAsync(ExternalLoginKey(provider, login.ProviderKey), user.UserId);
            }
        }

        if (!await transaction.ExecuteAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException("Failed to create user account.");
        }

        _logger.LogInformation("Created user account {UserId}", user.UserId);
    }

    public async Task UpdateAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisUserRepository.Update", ActivityKind.Client);
        activity?.SetTag("auth.user_id", user.UserId);

        var existing = await FindByIdAsync(user.UserId, cancellationToken).ConfigureAwait(false);
        var database = GetDatabase();
        var transaction = database.CreateTransaction();

        _ = transaction.StringSetAsync(UserKey(user.UserId), SerializeUser(user));
        _ = transaction.StringSetAsync(EmailKey(user.Email), user.UserId);

        if (existing is not null &&
            !string.Equals(existing.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            _ = transaction.KeyDeleteAsync(EmailKey(NormalizeEmail(existing.Email)));
        }

        foreach (var login in user.ExternalLogins)
        {
            if (Enum.TryParse<AuthProviderKind>(login.Provider, true, out var provider))
            {
                _ = transaction.StringSetAsync(ExternalLoginKey(provider, login.ProviderKey), user.UserId);
            }
        }

        if (!await transaction.ExecuteAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException("Failed to update user account.");
        }

        _logger.LogInformation("Updated user account {UserId}", user.UserId);
    }

    public async Task<bool> SoftDeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RedisUserRepository.SoftDelete", ActivityKind.Client);
        activity?.SetTag("auth.user_id", userId);

        var user = await FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDeleted)
        {
            return false;
        }

        var deleted = user.SoftDelete(DateTimeOffset.UtcNow);
        await UpdateAsync(deleted, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Soft-deleted user account {UserId}", userId);
        return true;
    }

    private IDatabase GetDatabase() => _connectionMultiplexer.GetDatabase();

    private string UserKey(string userId) => $"{_redisOptions.InstanceName}auth:user:{userId}";

    private string EmailKey(string email) => $"{_redisOptions.InstanceName}auth:email:{email}";

    private string ExternalLoginKey(AuthProviderKind provider, string providerKey) =>
        $"{_redisOptions.InstanceName}auth:external:{provider.ToString().ToLowerInvariant()}:{providerKey}";

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string SerializeUser(UserAccount user) => JsonSerializer.Serialize(user);

    private static UserAccount DeserializeUser(string payload) =>
        JsonSerializer.Deserialize<UserAccount>(payload)
        ?? throw new InvalidOperationException("Stored user account payload is invalid.");
}
