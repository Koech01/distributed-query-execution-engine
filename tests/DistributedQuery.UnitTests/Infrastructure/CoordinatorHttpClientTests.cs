using System.Net;
using System.Text.Json;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Coordinator;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.Infrastructure;

public sealed class CoordinatorHttpClientTests
{
    [Fact]
    public async Task ExecuteAsync_DeserializesQueryResult()
    {
        var queryId = Guid.NewGuid();
        var result = QueryResult.Create(
            queryId,
            ["id"],
            [["1"]],
            totalShards: 1,
            failedShards: [],
            executionMs: 5);

        var handler = new StubHttpMessageHandler(_ =>
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        });

        var client = new CoordinatorHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://coordinator") },
            Options.Create(new CoordinatorClientOptions { BaseUrl = "http://coordinator" }),
            NullLogger<CoordinatorHttpClient>.Instance);

        var request = QueryRequest.Create("SELECT 1");
        var actual = await client.ExecuteAsync(request);

        actual.QueryId.Should().Be(queryId);
        actual.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsInsufficientNodes_On503()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""
                    {
                      "type": "InsufficientNodesException",
                      "message": "not enough nodes",
                      "requiredShards": 4,
                      "availableNodes": 1
                    }
                    """)
            });

        var client = new CoordinatorHttpClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://coordinator") },
            Options.Create(new CoordinatorClientOptions { BaseUrl = "http://coordinator" }),
            NullLogger<CoordinatorHttpClient>.Instance);

        var act = () => client.ExecuteAsync(QueryRequest.Create("SELECT 1"));

        await act.Should().ThrowAsync<InsufficientNodesException>();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
