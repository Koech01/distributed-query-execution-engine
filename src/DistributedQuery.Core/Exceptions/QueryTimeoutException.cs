namespace DistributedQuery.Core.Exceptions;

public sealed class QueryTimeoutException : Exception
{
    public Guid QueryId { get; }
    public TimeSpan Timeout { get; }

    public QueryTimeoutException(Guid queryId, TimeSpan timeout)
        : base($"Query {queryId} timed out after {timeout.TotalMilliseconds}ms.")
    {
        QueryId = queryId;
        Timeout = timeout;
    }
}
