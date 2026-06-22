using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface IResultMerger
{
    QueryResult Merge(
        Guid queryId,
        IReadOnlyList<PartialResult> partialResults,
        MergeInstructions instructions,
        long totalExecutionMs);

    IAsyncEnumerable<QueryStreamEvent> StreamMergeAsync(
        Guid queryId,
        IAsyncEnumerable<PartialResult> partialResults,
        MergeInstructions instructions,
        CancellationToken cancellationToken = default);
}
