using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Infrastructure.Messaging;

public sealed class MassTransitQueryCompletionNotifier : IAsyncQueryCompletionNotifier
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitQueryCompletionNotifier> _logger;

    public MassTransitQueryCompletionNotifier(
        IPublishEndpoint publishEndpoint,
        ILogger<MassTransitQueryCompletionNotifier> logger)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyCompletedAsync(QueryCompleted completion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completion);

        await _publishEndpoint.Publish(completion, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Published async query completion for query {QueryId}, success={Success}",
            completion.QueryId,
            completion.Success);
    }
}
