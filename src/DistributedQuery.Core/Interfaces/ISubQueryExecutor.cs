using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface ISubQueryExecutor
{
    IAsyncEnumerable<PartialResult> ExecuteAsync(
        SubQuery subQuery,
        CancellationToken cancellationToken = default);
}
