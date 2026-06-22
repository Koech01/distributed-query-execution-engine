using System.Diagnostics;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Messages;
using DistributedQuery.Core.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Infrastructure.Messaging;

public sealed class SubQueryConsumer : IConsumer<SubQueryDispatched>
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Messaging.SubQueryConsumer");
    private const int SupportedSchemaVersion = 1;

    private readonly ISubQueryExecutor _subQueryExecutor;
    private readonly ILogger<SubQueryConsumer> _logger;

    public SubQueryConsumer(
        ISubQueryExecutor subQueryExecutor,
        ILogger<SubQueryConsumer> logger)
    {
        _subQueryExecutor = subQueryExecutor ?? throw new ArgumentNullException(nameof(subQueryExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<SubQueryDispatched> context)
    {
        var message = context.Message;
        using var activity = StartConsumerActivity(message);
        activity?.SetTag("query.id", message.ParentQueryId.ToString());
        activity?.SetTag("sub_query_id", message.SubQueryId.ToString());
        activity?.SetTag("shard.index", message.ShardIndex);

        if (message.SchemaVersion != SupportedSchemaVersion)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "unsupported_schema_version");
            _logger.LogWarning(
                "Rejected sub-query message {SubQueryId} for query {QueryId} with unsupported schema version {SchemaVersion}",
                message.SubQueryId,
                message.ParentQueryId,
                message.SchemaVersion);
            throw new InvalidOperationException($"Unsupported SubQueryDispatched schema version {message.SchemaVersion}.");
        }

        var subQuery = new SubQuery(
            message.SubQueryId,
            message.ParentQueryId,
            message.Sql,
            string.Empty,
            message.ShardIndex,
            message.TotalShards,
            message.Parameters,
            message.TimeoutMs);

        try
        {
            await foreach (var partialResult in _subQueryExecutor.ExecuteAsync(subQuery, context.CancellationToken).ConfigureAwait(false))
            {
                await context.Publish(MapToMessage(partialResult, message.TotalShards), context.CancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Sub-query consumption was canceled");
            _logger.LogWarning(
                "Sub-query {SubQueryId} execution canceled on shard {ShardIndex}",
                message.SubQueryId,
                message.ShardIndex);

            await context.Publish(
                CreateFailureMessage(message, "Sub-query execution was canceled."),
                context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex,
                "Sub-query {SubQueryId} execution failed on shard {ShardIndex}",
                message.SubQueryId,
                message.ShardIndex);

            await context.Publish(
                CreateFailureMessage(message, "Worker failed to execute sub-query."),
                context.CancellationToken).ConfigureAwait(false);
        }
    }

    private static PartialResultReady CreateFailureMessage(SubQueryDispatched message, string errorMessage) =>
        new(
            message.SubQueryId,
            message.ParentQueryId,
            message.ShardIndex,
            Success: false,
            ErrorMessage: errorMessage,
            ColumnNames: Array.Empty<string>(),
            Rows: Array.Empty<IReadOnlyList<string>>(),
            ExecutionMs: 0,
            Activity.Current?.Id,
            Activity.Current?.TraceStateString,
            message.TotalShards);

    private static PartialResultReady MapToMessage(PartialResult result, int totalShards)
    {
        return new PartialResultReady(
            result.SubQueryId,
            result.ParentQueryId,
            result.ShardIndex,
            result.IsSuccess,
            result.ErrorMessage,
            result.Columns.Select(static column => column.Name).ToArray(),
            result.Rows,
            result.ExecutionMs,
            Activity.Current?.Id,
            Activity.Current?.TraceStateString,
            totalShards);
    }

    private static Activity? StartConsumerActivity(SubQueryDispatched message)
    {
        if (ActivityContext.TryParse(message.TraceParent, message.TraceState, out var parentContext))
        {
            return ActivitySource.StartActivity("SubQueryConsumer.Consume", ActivityKind.Consumer, parentContext);
        }

        return ActivitySource.StartActivity("SubQueryConsumer.Consume", ActivityKind.Consumer);
    }
}
