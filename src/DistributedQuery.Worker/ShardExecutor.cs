using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Worker;

public sealed class ShardExecutor : ISubQueryExecutor
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Worker.ShardExecutor");
    private static readonly AsyncLocal<string> CurrentSubQueryStatus = new();

    private readonly WorkerOptions _options;
    private readonly ShardConnectionResolver _connectionResolver;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ILogger<ShardExecutor> _logger;

    public ShardExecutor(
        IOptions<WorkerOptions> options,
        ShardConnectionResolver connectionResolver,
        ILogger<ShardExecutor> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _connectionResolver = connectionResolver ?? throw new ArgumentNullException(nameof(connectionResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _concurrencyGate = new SemaphoreSlim(
            Math.Max(1, _options.Execution.MaxConcurrentQueries),
            Math.Max(1, _options.Execution.MaxConcurrentQueries));
    }

    public async IAsyncEnumerable<PartialResult> ExecuteAsync(
        SubQuery subQuery,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (subQuery is null)
        {
            throw new ArgumentNullException(nameof(subQuery));
        }

        if (!_options.ShardIndices.Contains(subQuery.ShardIndex))
        {
            throw new ArgumentException(
                $"Shard index {subQuery.ShardIndex} is not owned by this worker.",
                nameof(subQuery));
        }

        var acquired = false;
        try
        {
            await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;

            var shardTag = new KeyValuePair<string, object?>("shard_index", subQuery.ShardIndex);
            DqeeMetrics.WorkerActiveQueries.Add(1, shardTag);
            DqeeMetrics.WorkerDbConnectionPoolSize.Add(1, shardTag);

            var stopwatch = Stopwatch.StartNew();
            var rowCount = 0L;
            CurrentSubQueryStatus.Value = "success";

            try
            {
                await foreach (var partialResult in ExecuteOnShardAsync(subQuery, cancellationToken).ConfigureAwait(false))
                {
                    rowCount += partialResult.Rows.Count;
                    yield return partialResult;
                }
            }
            finally
            {
                stopwatch.Stop();
                DqeeMetrics.WorkerDbConnectionPoolSize.Add(-1, shardTag);
                DqeeMetrics.WorkerActiveQueries.Add(-1, shardTag);
                DqeeMetrics.WorkerSubQueriesTotal.Add(
                    1,
                    shardTag,
                    new KeyValuePair<string, object?>("status", CurrentSubQueryStatus.Value ?? "success"));
                DqeeMetrics.WorkerSubqueryDurationSeconds.Record(stopwatch.Elapsed.TotalSeconds, shardTag);

                if (rowCount > 0)
                {
                    DqeeMetrics.WorkerRowsReturnedTotal.Add(rowCount, shardTag);
                }
            }
        }
        finally
        {
            if (acquired)
            {
                _concurrencyGate.Release();
            }
        }
    }

    private async IAsyncEnumerable<PartialResult> ExecuteOnShardAsync(
        SubQuery subQuery,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = ActivitySource.StartActivity("ShardExecutor.Execute", ActivityKind.Client);
        activity?.SetTag("query.id", subQuery.ParentQueryId.ToString("D"));
        activity?.SetTag("sub_query_id", subQuery.SubQueryId.ToString("D"));
        activity?.SetTag("shard.index", subQuery.ShardIndex);
        activity?.SetTag("shard.total", subQuery.TotalShards);
        activity?.SetTag("worker.node_id", _options.NodeId);

        _logger.LogInformation(
            "Executing sub-query {SubQueryId} on shard {ShardIndex}",
            subQuery.SubQueryId,
            subQuery.ShardIndex);

        await using var connection = _connectionResolver.CreateConnection(subQuery.ShardIndex);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = subQuery.Sql;
        command.CommandTimeout = Math.Max(1, _options.Execution.CommandTimeoutSeconds);
        BindParameters(command, subQuery.Parameters);

        DbDataReader reader;
        try
        {
            reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CurrentSubQueryStatus.Value = "failed";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex,
                "Sub-query {SubQueryId} failed on shard {ShardIndex}",
                subQuery.SubQueryId,
                subQuery.ShardIndex);

            throw new ShardExecutionException(
                subQuery.ShardIndex,
                subQuery.SubQueryId,
                "Shard SQL execution failed.",
                ex);
        }

        await using (reader)
        {
            var columns = ReadColumnDescriptors(reader);
            var chunkSize = Math.Max(1, _options.Execution.StreamChunkSize);
            var chunkRows = new List<IReadOnlyList<string>>(chunkSize);
            var isFirstChunk = true;

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                chunkRows.Add(ReadRow(reader));

                if (chunkRows.Count < chunkSize)
                {
                    continue;
                }

                yield return CreateChunkResult(
                    subQuery,
                    columns,
                    chunkRows.ToArray(),
                    stopwatch.ElapsedMilliseconds,
                    includeColumns: isFirstChunk);

                chunkRows.Clear();
                isFirstChunk = false;
            }

            if (chunkRows.Count > 0 || isFirstChunk)
            {
                yield return CreateChunkResult(
                    subQuery,
                    columns,
                    chunkRows.ToArray(),
                    stopwatch.ElapsedMilliseconds,
                    includeColumns: isFirstChunk);
            }
        }
    }

    private static PartialResult CreateChunkResult(
        SubQuery subQuery,
        IReadOnlyList<ColumnDescriptor> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        long executionMs,
        bool includeColumns)
    {
        return PartialResult.Success(
            subQuery.SubQueryId,
            subQuery.ParentQueryId,
            subQuery.ShardIndex,
            includeColumns ? columns : Array.Empty<ColumnDescriptor>(),
            rows,
            executionMs);
    }

    private static IReadOnlyList<ColumnDescriptor> ReadColumnDescriptors(DbDataReader reader)
    {
        var columns = new ColumnDescriptor[reader.FieldCount];
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var fieldType = reader.GetFieldType(index);
            var isNullable = !fieldType.IsValueType || Nullable.GetUnderlyingType(fieldType) is not null;

            columns[index] = new ColumnDescriptor(
                reader.GetName(index),
                reader.GetDataTypeName(index),
                isNullable);
        }

        return columns;
    }

    private static IReadOnlyList<string> ReadRow(DbDataReader reader)
    {
        var values = new string[reader.FieldCount];
        for (var index = 0; index < reader.FieldCount; index++)
        {
            values[index] = FormatCellValue(reader.IsDBNull(index) ? null : reader.GetValue(index));
        }

        return values;
    }

    private static string FormatCellValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => JsonSerializer.Serialize(text),
            DateTime dateTime => JsonSerializer.Serialize(dateTime),
            DateTimeOffset dateTimeOffset => JsonSerializer.Serialize(dateTimeOffset),
            bool boolean => JsonSerializer.Serialize(boolean),
            _ => JsonSerializer.Serialize(value)
        };
    }

    private static void BindParameters(DbCommand command, IReadOnlyList<QueryParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name.StartsWith("@", StringComparison.Ordinal)
                ? parameter.Name
                : $"@{parameter.Name}";
            dbParameter.Value = DeserializeParameterValue(parameter) ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }
    }

    private static object? DeserializeParameterValue(QueryParameter parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter.Value) ||
            string.Equals(parameter.Value, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var document = JsonDocument.Parse(parameter.Value);
        var root = document.RootElement;

        return parameter.Type.ToLowerInvariant() switch
        {
            "int" or "int32" => root.GetInt32(),
            "long" or "int64" => root.GetInt64(),
            "bool" or "boolean" => root.GetBoolean(),
            "double" => root.GetDouble(),
            "decimal" => root.GetDecimal(),
            "datetime" or "datetime2" => root.GetDateTime(),
            "guid" => root.GetGuid(),
            _ => root.ValueKind switch
            {
                JsonValueKind.String => root.GetString(),
                JsonValueKind.Number when root.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when root.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => root.GetRawText()
            }
        };
    }
}
