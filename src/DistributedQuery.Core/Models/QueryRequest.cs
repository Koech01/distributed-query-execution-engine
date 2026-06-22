namespace DistributedQuery.Core.Models;

public record QueryRequest(
    Guid QueryId,
    string Sql,
    IReadOnlyList<QueryParameter> Parameters,
    int? MaxNodes = null,
    TimeSpan? Timeout = null,
    bool Async = false,
    FailurePolicy FailurePolicy = FailurePolicy.BestEffort
)
{
    public static QueryRequest Create(
        string sql,
        IReadOnlyList<QueryParameter>? parameters = null,
        TimeSpan? timeout = null,
        bool async = false,
        FailurePolicy failurePolicy = FailurePolicy.BestEffort) =>
        new(
            Guid.NewGuid(),
            sql,
            parameters ?? [],
            MaxNodes: null,
            Timeout: timeout ?? TimeSpan.FromSeconds(30),
            Async: async,
            FailurePolicy: failurePolicy);
}

public enum FailurePolicy { BestEffort, StrictAll }
