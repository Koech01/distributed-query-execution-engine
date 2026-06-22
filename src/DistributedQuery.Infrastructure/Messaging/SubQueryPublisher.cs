using System.Diagnostics;
using DistributedQuery.Core.Messages;
using DistributedQuery.Core.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Infrastructure.Messaging;

public sealed class SubQueryPublisher
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Messaging.SubQueryPublisher");

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<SubQueryPublisher> _logger;

    public SubQueryPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<SubQueryPublisher> logger)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(SubQuery subQuery, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (subQuery is null)
        {
            throw new ArgumentNullException(nameof(subQuery));
        }

        using var activity = ActivitySource.StartActivity("SubQueryPublisher.Publish", ActivityKind.Producer);
        activity?.SetTag("query.id", subQuery.ParentQueryId.ToString());
        activity?.SetTag("sub_query_id", subQuery.SubQueryId.ToString());
        activity?.SetTag("shard.index", subQuery.ShardIndex);

        var traceParent = Activity.Current?.Id;
        var traceState = Activity.Current?.TraceStateString;

        var message = new SubQueryDispatched(
            subQuery.SubQueryId,
            subQuery.ParentQueryId,
            subQuery.Sql,
            subQuery.ShardIndex,
            subQuery.TotalShards,
            subQuery.Parameters,
            timeoutMs,
            traceParent,
            traceState);

        await _publishEndpoint.Publish(message, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Published sub-query {SubQueryId} for query {QueryId} and shard {ShardIndex}",
            subQuery.SubQueryId,
            subQuery.ParentQueryId,
            subQuery.ShardIndex);
    }
}
