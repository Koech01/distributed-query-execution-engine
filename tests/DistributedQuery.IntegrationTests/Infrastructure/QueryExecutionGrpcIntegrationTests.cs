using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Grpc;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CoreColumnDescriptor = DistributedQuery.Core.Models.ColumnDescriptor;

namespace DistributedQuery.IntegrationTests.Infrastructure;

public sealed class QueryExecutionGrpcIntegrationTests
{
    [Fact]
    public async Task ExecuteSubQuery_StreamsMetaAndChunksOverGrpc()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var subQueryId = Guid.NewGuid();
        var parentQueryId = Guid.NewGuid();
        var partialResult = PartialResult.Success(
            subQueryId,
            parentQueryId,
            shardIndex: 0,
            new[] { new CoreColumnDescriptor("id", "int", false) },
            new[] { new[] { "1" }, new[] { "2" } },
            executionMs: 7);

        var port = Random.Shared.Next(10000, 60000);
        var baseAddress = $"http://127.0.0.1:{port}";

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(baseAddress);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(port, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
        });
        builder.Services.AddSingleton<ISubQueryExecutor>(new FakeSubQueryExecutor(partialResult));
        builder.Services.AddSingleton<ILogger<QueryExecutionService>>(NullLogger<QueryExecutionService>.Instance);
        builder.Services.AddGrpc(options => options.Interceptors.Add<TracingServerInterceptor>());

        var app = builder.Build();
        app.MapGrpcService<QueryExecutionService>();
        await app.StartAsync();

        try
        {
            using var channel = GrpcChannel.ForAddress(baseAddress);
            var client = new QueryExecution.QueryExecutionClient(channel);
            var request = new SubQueryRequest
            {
                SubQueryId = subQueryId.ToString("D"),
                ParentQueryId = parentQueryId.ToString("D"),
                Sql = "SELECT id FROM users",
                ShardIndex = 0,
                TotalShards = 4,
                TimeoutMs = 1000
            };

            using var call = client.ExecuteSubQuery(request);
            var messages = new List<PartialResultResponse>();
            await foreach (var message in call.ResponseStream.ReadAllAsync())
            {
                messages.Add(message);
            }

            messages.Should().HaveCountGreaterThanOrEqualTo(2);
            messages[0].PayloadCase.Should().Be(PartialResultResponse.PayloadOneofCase.Meta);
            messages.Skip(1).Should().AllSatisfy(m =>
                m.PayloadCase.Should().Be(PartialResultResponse.PayloadOneofCase.Chunk));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class FakeSubQueryExecutor : ISubQueryExecutor
    {
        private readonly PartialResult _partialResult;

        public FakeSubQueryExecutor(PartialResult partialResult)
        {
            _partialResult = partialResult;
        }

        public async IAsyncEnumerable<PartialResult> ExecuteAsync(
            SubQuery subQuery,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return _partialResult;
            await Task.CompletedTask;
        }
    }
}
