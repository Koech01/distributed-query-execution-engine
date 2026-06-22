using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface IQueryCache
{
    Task<QueryPlan?> TryGetPlanAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task SetPlanAsync(string cacheKey, QueryPlan plan, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task<QueryResult?> TryGetResultAsync(Guid queryId, CancellationToken cancellationToken = default);
    Task SetResultAsync(Guid queryId, QueryResult result, TimeSpan ttl, CancellationToken cancellationToken = default);
}
