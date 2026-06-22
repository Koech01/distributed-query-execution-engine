namespace DistributedQuery.Core.Models;

public sealed record CancelQueryResult(
    Guid QueryId,
    bool Found,
    bool CancellationRequested,
    string Message);
