using System.Diagnostics;
using System.Text.Json;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DistributedQuery.Infrastructure.Messaging;

public sealed class PartialResultConsumer : IConsumer<PartialResultReady>
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Messaging.PartialResultConsumer");
    private const int SupportedSchemaVersion = 1;

    private readonly IConnectionMultiplexer _redis;
    private readonly IAsyncQueryCompletionNotifier _completionNotifier;
    private readonly MessagingOptions _messagingOptions;
    private readonly ILogger<PartialResultConsumer> _logger;

    public PartialResultConsumer(
        IConnectionMultiplexer redis,
        IAsyncQueryCompletionNotifier completionNotifier,
        IOptions<MessagingOptions> messagingOptions,
        ILogger<PartialResultConsumer> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _completionNotifier = completionNotifier ?? throw new ArgumentNullException(nameof(completionNotifier));
        _messagingOptions = messagingOptions?.Value ?? throw new ArgumentNullException(nameof(messagingOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<PartialResultReady> context)
    {
        var message = context.Message;
        using var activity = StartConsumerActivity(message);
        activity?.SetTag("query.id", message.ParentQueryId.ToString());
        activity?.SetTag("sub_query_id", message.SubQueryId.ToString());
        activity?.SetTag("shard.index", message.ShardIndex);

        if (message.SchemaVersion != SupportedSchemaVersion)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "unsupported_schema_version");
            _logger.LogWarning(
                "Rejected partial result for query {QueryId}, shard {ShardIndex} with unsupported schema version {SchemaVersion}",
                message.ParentQueryId,
                message.ShardIndex,
                message.SchemaVersion);
            throw new InvalidOperationException($"Unsupported PartialResultReady schema version {message.SchemaVersion}.");
        }

        try
        {
            var db = _redis.GetDatabase();
            var partialsKey = $"query:{message.ParentQueryId:D}:partials";
            var mergeLockKey = $"query:{message.ParentQueryId:D}:merge-lock";
            var shardField = message.ShardIndex.ToString();
            var serialized = JsonSerializer.Serialize(message);
            var ttl = TimeSpan.FromHours(Math.Max(1, _messagingOptions.ResultRendezvousTtlHours));

            await db.HashSetAsync(partialsKey, shardField, serialized).WaitAsync(context.CancellationToken).ConfigureAwait(false);
            await db.KeyExpireAsync(partialsKey, ttl).WaitAsync(context.CancellationToken).ConfigureAwait(false);
            var receivedCount = await db.HashLengthAsync(partialsKey).WaitAsync(context.CancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Stored partial result for query {QueryId}, shard {ShardIndex}, success {Success}",
                message.ParentQueryId,
                message.ShardIndex,
                message.Success);

            if (message.TotalShards > 0 && receivedCount >= message.TotalShards)
            {
                var lockAcquired = await db.LockTakeAsync(
                        mergeLockKey,
                        Environment.MachineName,
                        TimeSpan.FromSeconds(Math.Max(1, _messagingOptions.MergeLockTtlSeconds)))
                    .WaitAsync(context.CancellationToken)
                    .ConfigureAwait(false);

                if (!lockAcquired)
                {
                    _logger.LogDebug(
                        "Async completion for query {QueryId} is already being handled by another consumer",
                        message.ParentQueryId);
                    return;
                }

                var allPartials = await db.HashValuesAsync(partialsKey).WaitAsync(context.CancellationToken).ConfigureAwait(false);
                var allSucceeded = AllPartialsSucceeded(allPartials);
                await _completionNotifier.NotifyCompletedAsync(
                    new QueryCompleted(
                        message.ParentQueryId,
                        allSucceeded,
                        allSucceeded ? null : "One or more async partial results failed."),
                    context.CancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "All {TotalShards} async partial results received for query {QueryId}",
                    message.TotalShards,
                    message.ParentQueryId);
            }
        }
        catch (RedisException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex,
                "Redis rendezvous failed for async partial result. QueryId={QueryId}, ShardIndex={ShardIndex}",
                message.ParentQueryId,
                message.ShardIndex);
            throw;
        }
    }

    private static Activity? StartConsumerActivity(PartialResultReady message)
    {
        if (ActivityContext.TryParse(message.TraceParent, message.TraceState, out var parentContext))
        {
            return ActivitySource.StartActivity("PartialResultConsumer.Consume", ActivityKind.Consumer, parentContext);
        }

        return ActivitySource.StartActivity("PartialResultConsumer.Consume", ActivityKind.Consumer);
    }

    private static bool AllPartialsSucceeded(IReadOnlyList<RedisValue> serializedPartials)
    {
        if (serializedPartials.Count == 0)
        {
            return false;
        }

        foreach (var serialized in serializedPartials)
        {
            var partial = JsonSerializer.Deserialize<PartialResultReady>(serialized.ToString());
            if (partial?.Success != true)
            {
                return false;
            }
        }

        return true;
    }
}
