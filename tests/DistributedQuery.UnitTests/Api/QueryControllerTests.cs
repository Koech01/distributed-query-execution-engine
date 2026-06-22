using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistributedQuery.Core.Models;
using FluentAssertions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Api;

public sealed class QueryControllerTests : IClassFixture<QueryApiWebApplicationFactory>
{
    private readonly QueryApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public QueryControllerTests(QueryApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostQueries_ReturnsOk_WhenCoordinatorReturnsResult()
    {
        var queryId = Guid.NewGuid();
        var result = QueryResult.Create(
            queryId,
            ["id"],
            [["1"]],
            totalShards: 1,
            failedShards: [],
            executionMs: 12);

        _factory.CoordinatorClient
            .ExecuteAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _client.PostAsJsonAsync("/queries", new
        {
            sql = "SELECT 1",
            failurePolicy = "BestEffort"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<QueryResult>();
        payload!.QueryId.Should().Be(queryId);
        payload.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task PostQueries_Returns206_WhenResultIsDegraded()
    {
        var queryId = Guid.NewGuid();
        var result = QueryResult.Create(
            queryId,
            ["id"],
            [["1"]],
            totalShards: 2,
            failedShards: [1],
            executionMs: 20);

        _factory.CoordinatorClient
            .ExecuteAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _client.PostAsJsonAsync("/queries", new
        {
            sql = "SELECT 1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
    }

    [Fact]
    public async Task PostQueries_ReturnsAccepted_WhenAsyncIsTrue()
    {
        using var content = new StringContent(
            """{"sql":"SELECT 1","async":true}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/queries", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("queryId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("statusUrl").GetString().Should().StartWith("/queries/");
    }

    [Fact]
    public async Task GetStatus_ReturnsCompleted_WhenResultIsCached()
    {
        var queryId = Guid.NewGuid();
        var result = QueryResult.Create(
            queryId,
            ["id"],
            [],
            totalShards: 1,
            failedShards: [],
            executionMs: 1);

        _factory.QueryCache
            .TryGetResultAsync(queryId, Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _client.GetAsync($"/queries/{queryId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task GetResult_ReturnsNotFound_WhenCacheMisses()
    {
        var queryId = Guid.NewGuid();

        _factory.QueryCache
            .TryGetResultAsync(queryId, Arg.Any<CancellationToken>())
            .Returns((QueryResult?)null);

        var response = await _client.GetAsync($"/queries/{queryId}/result");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostQueriesPlan_ReturnsPlanDetails()
    {
        var queryId = Guid.NewGuid();
        var plan = new QueryPlanDetails(
            Guid.NewGuid(),
            "hash",
            FromCache: true,
            "broadcast",
            2,
            [new QueryPlanSubQueryDetails(Guid.NewGuid(), 0, 2, "SELECT id FROM orders")],
            new QueryPlanMergeDetails([], [], null, null, false),
            DateTimeOffset.UtcNow);

        _factory.CoordinatorClient
            .PlanAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(plan);

        var response = await _client.PostAsJsonAsync("/queries/plan", new
        {
            queryId,
            sql = "SELECT id FROM orders"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<QueryPlanDetails>();
        payload!.PlanHash.Should().Be("hash");
        payload.TargetingStrategy.Should().Be("broadcast");
        payload.FromCache.Should().BeTrue();
    }

    [Fact]
    public async Task PostQueriesStream_ReturnsEventStream()
    {
        var queryId = Guid.NewGuid();
        async IAsyncEnumerable<QueryStreamEvent> ProduceEvents()
        {
            yield return new QueryStreamEvent(
                QueryStreamEventKind.Metadata,
                QueryId: queryId,
                TotalShards: 1,
                StreamMode: QueryStreamMode.Incremental);
            yield return new QueryStreamEvent(QueryStreamEventKind.Columns, Columns: ["id"]);
            yield return new QueryStreamEvent(QueryStreamEventKind.Row, Row: ["1"]);
            yield return new QueryStreamEvent(
                QueryStreamEventKind.Complete,
                Complete: new QueryStreamCompletePayload(1, 1, 1, [], false, null, 5));
        }

        _factory.CoordinatorClient
            .StreamExecuteAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(ProduceEvents());

        using var request = new HttpRequestMessage(HttpMethod.Post, "/queries/stream")
        {
            Content = JsonContent.Create(new { sql = "SELECT 1" })
        };

        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("event: metadata");
        body.Should().Contain("event: row");
        body.Should().Contain("event: complete");
    }

    [Fact]
    public async Task PostQueriesStream_ReturnsBadRequest_WhenAsyncIsTrue()
    {
        var response = await _client.PostAsJsonAsync("/queries/stream", new
        {
            sql = "SELECT 1",
            async = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
