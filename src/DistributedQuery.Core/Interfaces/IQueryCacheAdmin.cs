using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

/// <summary>
/// Administrative cache operations backed by Redis. Infrastructure-only I/O; no business logic.
/// </summary>
public interface IQueryCacheAdmin
{
    Task<AdminCacheStats> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<AdminCacheFlushResult> FlushPlansAsync(
        string? planHash = null,
        CancellationToken cancellationToken = default);
}
