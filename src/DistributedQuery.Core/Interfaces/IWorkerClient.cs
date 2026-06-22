using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface IWorkerClient
{
    IAsyncEnumerable<PartialResult> ExecuteAsync(
        SubQuery subQuery,
        NodeInfo node,
        CancellationToken cancellationToken = default);
}
