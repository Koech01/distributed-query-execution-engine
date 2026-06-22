using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using DistributedQuery.Core.Models;

namespace DistributedQuery.Infrastructure.Coordinator;

public static class ServerSentEventStreamReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async IAsyncEnumerable<QueryStreamEvent> ReadEventsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);

        string? eventName = null;
        var dataLines = new List<string>();

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                if (eventName is not null && dataLines.Count > 0)
                {
                    var parsed = ParseEvent(eventName, string.Join('\n', dataLines));
                    if (parsed is not null)
                    {
                        yield return parsed;
                    }
                }

                eventName = null;
                dataLines.Clear();
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line["event:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line["data:".Length..].Trim());
            }
        }

        if (eventName is not null && dataLines.Count > 0)
        {
            var parsed = ParseEvent(eventName, string.Join('\n', dataLines));
            if (parsed is not null)
            {
                yield return parsed;
            }
        }
    }

    private static QueryStreamEvent? ParseEvent(string eventName, string dataJson)
    {
        return eventName switch
        {
            "metadata" => DeserializeMetadata(dataJson),
            "columns" => DeserializeColumns(dataJson),
            "row" => DeserializeRow(dataJson),
            "complete" => DeserializeComplete(dataJson),
            _ => null
        };
    }

    private static QueryStreamEvent DeserializeMetadata(string dataJson)
    {
        var payload = JsonSerializer.Deserialize<MetadataPayload>(dataJson, JsonOptions)
            ?? throw new InvalidOperationException("Streaming metadata payload was empty.");

        return new QueryStreamEvent(
            QueryStreamEventKind.Metadata,
            QueryId: payload.QueryId,
            TotalShards: payload.TotalShards,
            StreamMode: payload.StreamMode);
    }

    private static QueryStreamEvent DeserializeColumns(string dataJson)
    {
        var payload = JsonSerializer.Deserialize<ColumnsPayload>(dataJson, JsonOptions)
            ?? throw new InvalidOperationException("Streaming columns payload was empty.");

        return new QueryStreamEvent(
            QueryStreamEventKind.Columns,
            Columns: payload.Columns);
    }

    private static QueryStreamEvent DeserializeRow(string dataJson)
    {
        var payload = JsonSerializer.Deserialize<RowPayload>(dataJson, JsonOptions)
            ?? throw new InvalidOperationException("Streaming row payload was empty.");

        return new QueryStreamEvent(
            QueryStreamEventKind.Row,
            Row: payload.Values);
    }

    private static QueryStreamEvent DeserializeComplete(string dataJson)
    {
        var payload = JsonSerializer.Deserialize<CompletePayload>(dataJson, JsonOptions)
            ?? throw new InvalidOperationException("Streaming complete payload was empty.");

        return new QueryStreamEvent(
            QueryStreamEventKind.Complete,
            Complete: new QueryStreamCompletePayload(
                payload.RowCount,
                payload.TotalShards,
                payload.SuccessfulShards,
                payload.FailedShards ?? [],
                payload.Degraded,
                payload.DegradationReason,
                payload.ExecutionMs));
    }

    private sealed record MetadataPayload(Guid QueryId, int TotalShards, QueryStreamMode StreamMode);

    private sealed record ColumnsPayload(IReadOnlyList<string> Columns);

    private sealed record RowPayload(IReadOnlyList<string> Values);

    private sealed record CompletePayload(
        int RowCount,
        int TotalShards,
        int SuccessfulShards,
        IReadOnlyList<int>? FailedShards,
        bool Degraded,
        string? DegradationReason,
        long ExecutionMs);
}
