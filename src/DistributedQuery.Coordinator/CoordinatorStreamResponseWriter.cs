using System.Text.Json;
using DistributedQuery.Core.Models;

namespace DistributedQuery.Coordinator;

public static class CoordinatorStreamResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task WriteEventAsync(
        HttpResponse response,
        QueryStreamEvent streamEvent,
        CancellationToken cancellationToken)
    {
        switch (streamEvent.Kind)
        {
            case QueryStreamEventKind.Metadata:
                await WriteNamedEventAsync(
                    response,
                    "metadata",
                    new
                    {
                        queryId = streamEvent.QueryId,
                        totalShards = streamEvent.TotalShards,
                        streamMode = streamEvent.StreamMode?.ToString().ToLowerInvariant()
                    },
                    cancellationToken).ConfigureAwait(false);
                break;
            case QueryStreamEventKind.Columns:
                await WriteNamedEventAsync(
                    response,
                    "columns",
                    new { columns = streamEvent.Columns ?? [] },
                    cancellationToken).ConfigureAwait(false);
                break;
            case QueryStreamEventKind.Row:
                await WriteNamedEventAsync(
                    response,
                    "row",
                    new { values = streamEvent.Row ?? [] },
                    cancellationToken).ConfigureAwait(false);
                break;
            case QueryStreamEventKind.Complete:
                await WriteNamedEventAsync(
                    response,
                    "complete",
                    new
                    {
                        rowCount = streamEvent.Complete?.RowCount,
                        totalShards = streamEvent.Complete?.TotalShards,
                        successfulShards = streamEvent.Complete?.SuccessfulShards,
                        failedShards = streamEvent.Complete?.FailedShards,
                        degraded = streamEvent.Complete?.Degraded,
                        degradationReason = streamEvent.Complete?.DegradationReason,
                        executionMs = streamEvent.Complete?.ExecutionMs
                    },
                    cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static async Task WriteNamedEventAsync(
        HttpResponse response,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await response.WriteAsync($"event: {eventName}\n", cancellationToken).ConfigureAwait(false);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
