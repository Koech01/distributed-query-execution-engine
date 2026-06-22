using System.Text;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Coordinator;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Infrastructure;

public sealed class ServerSentEventStreamReaderTests
{
    [Fact]
    public async Task ReadEventsAsync_ParsesMetadataColumnsRowAndCompleteEvents()
    {
        const string payload = """
            event: metadata
            data: {"queryId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","totalShards":2,"streamMode":"ordered"}

            event: columns
            data: {"columns":["id","amount"]}

            event: row
            data: {"values":["1","10"]}

            event: complete
            data: {"rowCount":1,"totalShards":2,"successfulShards":2,"failedShards":[],"degraded":false,"executionMs":12}

            """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var events = new List<QueryStreamEvent>();
        await foreach (var streamEvent in ServerSentEventStreamReader.ReadEventsAsync(stream))
        {
            events.Add(streamEvent);
        }

        events.Should().HaveCount(4);
        events[0].Kind.Should().Be(QueryStreamEventKind.Metadata);
        events[0].QueryId.Should().Be(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"));
        events[0].StreamMode.Should().Be(QueryStreamMode.Ordered);
        events[1].Columns.Should().Equal("id", "amount");
        events[2].Row.Should().Equal("1", "10");
        events[3].Complete!.RowCount.Should().Be(1);
    }
}
