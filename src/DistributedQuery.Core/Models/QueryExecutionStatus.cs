namespace DistributedQuery.Core.Models;

public enum QueryExecutionState
{
    Pending,
    Running,
    Completed,
    Failed
}

public sealed record QueryExecutionStatus(
    Guid QueryId,
    QueryExecutionState State,
    string? Message = null)
{
    public static QueryExecutionStatus Pending(Guid queryId) =>
        new(queryId, QueryExecutionState.Pending);

    public static QueryExecutionStatus Running(Guid queryId) =>
        new(queryId, QueryExecutionState.Running);

    public static QueryExecutionStatus Completed(Guid queryId) =>
        new(queryId, QueryExecutionState.Completed);

    public static QueryExecutionStatus Failed(Guid queryId, string message) =>
        new(queryId, QueryExecutionState.Failed, message);
}
