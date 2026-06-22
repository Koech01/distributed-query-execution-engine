namespace DistributedQuery.Core.Models;

public enum PartialResultStatus { Success, Failed, TimedOut, Degraded }

public record ColumnDescriptor(string Name, string DataType, bool Nullable);

public record PartialResult(
    Guid SubQueryId,
    Guid ParentQueryId,
    int ShardIndex,
    PartialResultStatus Status,
    IReadOnlyList<ColumnDescriptor> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    long ExecutionMs,
    string? ErrorMessage
)
{
    public bool IsSuccess => Status == PartialResultStatus.Success;

    public static PartialResult Success(
        Guid subQueryId,
        Guid parentQueryId,
        int shardIndex,
        IReadOnlyList<ColumnDescriptor> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        long executionMs) =>
        new(subQueryId, parentQueryId, shardIndex, PartialResultStatus.Success,
            columns, rows, executionMs, null);

    public static PartialResult Failure(
        Guid subQueryId,
        Guid parentQueryId,
        int shardIndex,
        PartialResultStatus status,
        string errorMessage) =>
        new(subQueryId, parentQueryId, shardIndex, status,
            [], [], 0, errorMessage);
}
