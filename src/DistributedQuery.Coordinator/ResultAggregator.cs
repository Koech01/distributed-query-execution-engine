using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Coordinator;

public sealed class ResultAggregator : IResultMerger
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Coordinator");
    private readonly ILogger<ResultAggregator> _logger;

    public ResultAggregator(ILogger<ResultAggregator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public QueryResult Merge(
        Guid queryId,
        IReadOnlyList<PartialResult> partialResults,
        MergeInstructions instructions,
        long totalExecutionMs)
    {
        ArgumentNullException.ThrowIfNull(partialResults);
        ArgumentNullException.ThrowIfNull(instructions);

        using var activity = ActivitySource.StartActivity("coordinator.results.merge", ActivityKind.Internal);
        activity?.SetTag("query.id", queryId.ToString("D"));

        var mergeStopwatch = Stopwatch.StartNew();
        var successful = partialResults.Where(static result => result.IsSuccess).ToList();
        var failedShards = partialResults
            .Where(static result => !result.IsSuccess)
            .Select(static result => result.ShardIndex)
            .Distinct()
            .OrderBy(static index => index)
            .ToArray();

        var totalShards = partialResults
            .Select(static result => result.ShardIndex)
            .Distinct()
            .Count();

        IReadOnlyList<IReadOnlyList<string>> mergedRows;
        IReadOnlyList<string> columns;

        if (instructions.Aggregates.Count > 0)
        {
            var aggregateResult = MergeAggregates(successful, instructions.Aggregates);
            mergedRows = aggregateResult.Rows;
            columns = aggregateResult.Columns;
        }
        else
        {
            var rowSets = successful.Select(static result => result.Rows).ToList();
            var baseRows = instructions.OrderBy.Count > 0
                ? MergeOrderedRows(successful, instructions.OrderBy)
                : rowSets.SelectMany(static rows => rows).ToList();

            if (instructions.IsDistinct)
            {
                baseRows = ApplyDistinct(baseRows);
            }

            baseRows = ApplyOffsetAndLimit(baseRows, instructions.Offset, instructions.Limit);
            mergedRows = baseRows;
            columns = successful.FirstOrDefault()?.Columns.Select(static c => c.Name).ToArray() ?? [];
        }

        mergeStopwatch.Stop();
        CoordinatorObservability.RecordMergeDuration(mergeStopwatch.Elapsed);

        activity?.SetTag("result.row_count", mergedRows.Count);
        activity?.SetTag("result.degraded", failedShards.Length > 0);

        _logger.LogInformation(
            "Merged query result for query {QueryId}. Rows={RowCount}, FailedShards={FailedShardCount}",
            queryId,
            mergedRows.Count,
            failedShards.Length);

        return QueryResult.Create(queryId, columns, mergedRows, totalShards, failedShards, totalExecutionMs);
    }

    public async IAsyncEnumerable<QueryStreamEvent> StreamMergeAsync(
        Guid queryId,
        IAsyncEnumerable<PartialResult> partialResults,
        MergeInstructions instructions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partialResults);
        ArgumentNullException.ThrowIfNull(instructions);

        using var activity = ActivitySource.StartActivity("coordinator.results.stream_merge", ActivityKind.Internal);
        activity?.SetTag("query.id", queryId.ToString("D"));

        var streamMode = QueryPlanMapper.ResolveStreamMode(instructions);
        activity?.SetTag("stream.mode", streamMode.ToString());

        if (streamMode == QueryStreamMode.Incremental)
        {
            var columnsSent = false;
            await foreach (var partial in partialResults.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (!partial.IsSuccess)
                {
                    continue;
                }

                if (!columnsSent && partial.Columns.Count > 0)
                {
                    var columnNames = partial.Columns.Select(static column => column.Name).ToArray();
                    yield return new QueryStreamEvent(QueryStreamEventKind.Columns, Columns: columnNames);
                    columnsSent = true;
                }

                foreach (var row in partial.Rows)
                {
                    yield return new QueryStreamEvent(QueryStreamEventKind.Row, Row: row);
                }
            }

            yield break;
        }

        var collected = new List<PartialResult>();
        await foreach (var partial in partialResults.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            collected.Add(partial);
        }

        if (streamMode == QueryStreamMode.Ordered)
        {
            var successful = collected.Where(static result => result.IsSuccess).ToList();
            var columns = successful.FirstOrDefault()?.Columns.Select(static column => column.Name).ToArray() ?? [];
            if (columns.Length > 0)
            {
                yield return new QueryStreamEvent(QueryStreamEventKind.Columns, Columns: columns);
            }

            foreach (var row in MergeOrderedRows(successful, instructions.OrderBy))
            {
                yield return new QueryStreamEvent(QueryStreamEventKind.Row, Row: row);
            }

            yield break;
        }

        var merged = Merge(queryId, collected, instructions, totalExecutionMs: 0);
        if (merged.Columns.Count > 0)
        {
            yield return new QueryStreamEvent(QueryStreamEventKind.Columns, Columns: merged.Columns);
        }

        foreach (var row in merged.Rows)
        {
            yield return new QueryStreamEvent(QueryStreamEventKind.Row, Row: row);
        }
    }

    private static (IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string> Columns) MergeAggregates(
        IReadOnlyList<PartialResult> successful,
        IReadOnlyList<AggregateOperation> operations)
    {
        if (successful.Count == 0)
        {
            return ([], operations.Select(static operation => operation.OutputAlias).ToArray());
        }

        var columnMap = BuildColumnMap(successful[0].Columns);
        var values = new List<string>(operations.Count);

        foreach (var operation in operations)
        {
            var mergedValue = operation.Function switch
            {
                AggregateFunction.Sum => MergeSum(successful, columnMap, operation.SourceColumn),
                AggregateFunction.Count => MergeSum(successful, columnMap, operation.SourceColumn),
                AggregateFunction.Min => MergeMin(successful, columnMap, operation.SourceColumn),
                AggregateFunction.Max => MergeMax(successful, columnMap, operation.SourceColumn),
                AggregateFunction.Avg => MergeAverage(successful, columnMap, operation),
                AggregateFunction.CountDistinct => MergeCountDistinct(successful, columnMap, operation.SourceColumn),
                _ => "0"
            };
            values.Add(mergedValue);
        }

        return ([values], operations.Select(static operation => operation.OutputAlias).ToArray());
    }

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<ColumnDescriptor> columns) =>
        columns
            .Select((column, index) => (column.Name, index))
            .ToDictionary(static item => item.Name, static item => item.index, StringComparer.OrdinalIgnoreCase);

    private static string MergeSum(IReadOnlyList<PartialResult> successful, IReadOnlyDictionary<string, int> columnMap, string columnName)
    {
        var sum = 0m;
        foreach (var value in EnumerateColumnValues(successful, columnMap, columnName))
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                sum += parsed;
            }
        }

        return sum.ToString(CultureInfo.InvariantCulture);
    }

    private static string MergeAverage(IReadOnlyList<PartialResult> successful, IReadOnlyDictionary<string, int> columnMap, AggregateOperation operation)
    {
        var sumAlias = $"__sum_{operation.SourceColumn}";
        var countAlias = $"__count_{operation.SourceColumn}";
        var sumColumn = columnMap.ContainsKey(sumAlias) ? sumAlias : operation.SourceColumn;
        var countColumn = columnMap.ContainsKey(countAlias)
            ? countAlias
            : operation.SourceColumn.EndsWith("_sum", StringComparison.OrdinalIgnoreCase)
                ? operation.SourceColumn[..^4] + "_count"
                : operation.OutputAlias.Replace("avg", "count", StringComparison.OrdinalIgnoreCase);

        var sum = 0m;
        var count = 0m;

        foreach (var value in EnumerateColumnValues(successful, columnMap, sumColumn))
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                sum += parsed;
            }
        }

        foreach (var value in EnumerateColumnValues(successful, columnMap, countColumn))
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                count += parsed;
            }
        }

        return count == 0m ? "0" : (sum / count).ToString(CultureInfo.InvariantCulture);
    }

    private static string MergeMin(IReadOnlyList<PartialResult> successful, IReadOnlyDictionary<string, int> columnMap, string columnName)
    {
        decimal? min = null;
        foreach (var value in EnumerateColumnValues(successful, columnMap, columnName))
        {
            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                continue;
            }

            min = min is null ? parsed : decimal.Min(min.Value, parsed);
        }

        return (min ?? 0m).ToString(CultureInfo.InvariantCulture);
    }

    private static string MergeMax(IReadOnlyList<PartialResult> successful, IReadOnlyDictionary<string, int> columnMap, string columnName)
    {
        decimal? max = null;
        foreach (var value in EnumerateColumnValues(successful, columnMap, columnName))
        {
            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                continue;
            }

            max = max is null ? parsed : decimal.Max(max.Value, parsed);
        }

        return (max ?? 0m).ToString(CultureInfo.InvariantCulture);
    }

    private static string MergeCountDistinct(IReadOnlyList<PartialResult> successful, IReadOnlyDictionary<string, int> columnMap, string columnName)
    {
        var distinct = EnumerateColumnValues(successful, columnMap, columnName)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();
        return distinct.ToString(CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> EnumerateColumnValues(
        IReadOnlyList<PartialResult> successful,
        IReadOnlyDictionary<string, int> columnMap,
        string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out var index))
        {
            yield break;
        }

        foreach (var result in successful)
        {
            foreach (var row in result.Rows)
            {
                if (index < row.Count)
                {
                    yield return row[index];
                }
            }
        }
    }

    private static List<IReadOnlyList<string>> MergeOrderedRows(
        IReadOnlyList<PartialResult> successful,
        IReadOnlyList<OrderByColumn> orderBy)
    {
        if (successful.Count == 0)
        {
            return [];
        }

        var columnMap = BuildColumnMap(successful[0].Columns);
        var queue = new PriorityQueue<(int ResultIndex, int RowIndex, IReadOnlyList<string> Row), RowSortKey>();

        for (var index = 0; index < successful.Count; index++)
        {
            var rows = successful[index].Rows;
            if (rows.Count == 0)
            {
                continue;
            }

            queue.Enqueue((index, 0, rows[0]), CreateSortKey(rows[0], orderBy, columnMap));
        }

        var merged = new List<IReadOnlyList<string>>();
        while (queue.TryDequeue(out var item, out _))
        {
            merged.Add(item.Row);

            var nextRowIndex = item.RowIndex + 1;
            var rows = successful[item.ResultIndex].Rows;
            if (nextRowIndex >= rows.Count)
            {
                continue;
            }

            var nextRow = rows[nextRowIndex];
            queue.Enqueue((item.ResultIndex, nextRowIndex, nextRow), CreateSortKey(nextRow, orderBy, columnMap));
        }

        return merged;
    }

    private static RowSortKey CreateSortKey(
        IReadOnlyList<string> row,
        IReadOnlyList<OrderByColumn> orderBy,
        IReadOnlyDictionary<string, int> columnMap)
    {
        var values = new List<string>(orderBy.Count);
        var descending = new List<bool>(orderBy.Count);
        foreach (var orderByColumn in orderBy)
        {
            columnMap.TryGetValue(orderByColumn.ColumnName, out var index);
            values.Add(index < row.Count ? row[index] : string.Empty);
            descending.Add(orderByColumn.Descending);
        }

        return new RowSortKey(values, descending);
    }

    private static List<IReadOnlyList<string>> ApplyDistinct(List<IReadOnlyList<string>> rows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new List<IReadOnlyList<string>>();
        foreach (var row in rows)
        {
            var key = string.Join('\u001f', row);
            if (seen.Add(key))
            {
                deduped.Add(row);
            }
        }

        return deduped;
    }

    private static List<IReadOnlyList<string>> ApplyOffsetAndLimit(List<IReadOnlyList<string>> rows, int? offset, int? limit)
    {
        var query = rows.AsEnumerable();
        if (offset is > 0)
        {
            query = query.Skip(offset.Value);
        }

        if (limit is > 0)
        {
            query = query.Take(limit.Value);
        }

        return query.ToList();
    }

    private sealed record RowSortKey(IReadOnlyList<string> Values, IReadOnlyList<bool> DescendingFlags) : IComparable<RowSortKey>
    {
        public int CompareTo(RowSortKey? other)
        {
            if (other is null)
            {
                return -1;
            }

            for (var index = 0; index < Values.Count; index++)
            {
                var left = Values[index];
                var right = other.Values[index];
                var comparison = CompareValue(left, right);
                if (comparison == 0)
                {
                    continue;
                }

                return DescendingFlags[index] ? -comparison : comparison;
            }

            return 0;
        }

        private static int CompareValue(string left, string right)
        {
            if (decimal.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftDecimal) &&
                decimal.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightDecimal))
            {
                return leftDecimal.CompareTo(rightDecimal);
            }

            return string.Compare(left, right, StringComparison.Ordinal);
        }
    }
}
