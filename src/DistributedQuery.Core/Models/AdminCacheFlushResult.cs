namespace DistributedQuery.Core.Models;

public sealed record AdminCacheFlushResult(
    long DeletedPlanEntries,
    string Scope,
    DateTimeOffset FlushedAt);
