namespace DistributedQuery.Core.Models;

public enum QueryStreamMode
{
    Incremental,
    Ordered,
    Buffered
}

public enum QueryStreamEventKind
{
    Metadata,
    Columns,
    Row,
    Complete
}

public record QueryStreamEvent(
    QueryStreamEventKind Kind,
    Guid? QueryId = null,
    int? TotalShards = null,
    QueryStreamMode? StreamMode = null,
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<string>? Row = null,
    QueryStreamCompletePayload? Complete = null);

public record QueryStreamCompletePayload(
    int RowCount,
    int TotalShards,
    int SuccessfulShards,
    IReadOnlyList<int> FailedShards,
    bool Degraded,
    string? DegradationReason,
    long ExecutionMs);
