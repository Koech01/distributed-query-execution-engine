namespace DistributedQuery.Core.Models;

public enum AggregateFunction { Sum, Count, Avg, Min, Max, CountDistinct }

public record OrderByColumn(string ColumnName, bool Descending);

public record AggregateOperation(
    AggregateFunction Function,
    string SourceColumn,
    string OutputAlias
);

public record MergeInstructions(
    IReadOnlyList<OrderByColumn> OrderBy,
    IReadOnlyList<AggregateOperation> Aggregates,
    int? Limit,
    int? Offset,
    bool IsDistinct
)
{
    public static MergeInstructions Empty { get; } = new(
        OrderBy: [],
        Aggregates: [],
        Limit: null,
        Offset: null,
        IsDistinct: false
    );
}
