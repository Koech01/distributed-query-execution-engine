using DistributedQuery.Core.Messages;

namespace DistributedQuery.Core.Interfaces;

public interface IAsyncQueryCompletionNotifier
{
    Task NotifyCompletedAsync(QueryCompleted completion, CancellationToken cancellationToken = default);
}
