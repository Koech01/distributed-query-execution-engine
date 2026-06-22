using System.Text.Json;
using DistributedQuery.Core.Models;

namespace DistributedQuery.Api.Services;

public static class QueryStreamResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task WriteEventAsync(
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

    public static Task WriteMetadataAsync(
        HttpResponse response,
        Guid queryId,
        int totalShards,
        QueryStreamMode streamMode,
        CancellationToken cancellationToken) =>
        WriteEventAsync(
            response,
            "metadata",
            new { queryId, totalShards, streamMode = streamMode.ToString().ToLowerInvariant() },
            cancellationToken);

    public static Task WriteColumnsAsync(
        HttpResponse response,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken) =>
        WriteEventAsync(response, "columns", new { columns }, cancellationToken);

    public static Task WriteRowAsync(
        HttpResponse response,
        IReadOnlyList<string> values,
        CancellationToken cancellationToken) =>
        WriteEventAsync(response, "row", new { values }, cancellationToken);

    public static Task WriteCompleteAsync(
        HttpResponse response,
        QueryStreamCompletePayload complete,
        CancellationToken cancellationToken) =>
        WriteEventAsync(
            response,
            "complete",
            new
            {
                rowCount = complete.RowCount,
                totalShards = complete.TotalShards,
                successfulShards = complete.SuccessfulShards,
                failedShards = complete.FailedShards,
                degraded = complete.Degraded,
                degradationReason = complete.DegradationReason,
                executionMs = complete.ExecutionMs
            },
            cancellationToken);
}
