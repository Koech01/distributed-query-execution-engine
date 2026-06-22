using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreColumnDescriptor = DistributedQuery.Core.Models.ColumnDescriptor;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Grpc;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DistributedQuery.UnitTests.Infrastructure;

public class QueryExecutionServiceTests
{
    [Fact]
    public async Task ExecuteSubQuery_WritesMetaAndChunks_WhenExecutorReturnsSuccess()
    {
        var partialResult = PartialResult.Success(
            Guid.NewGuid(),
            Guid.NewGuid(),
            shardIndex: 1,
            new[] { new CoreColumnDescriptor("id", "int", false) },
            new[] { new[] { "1" }, new[] { "2" } },
            executionMs: 15);

        var executor = Substitute.For<ISubQueryExecutor>();
        executor.ExecuteAsync(Arg.Any<SubQuery>(), Arg.Any<CancellationToken>())
            .Returns(GetAsyncEnumerable(partialResult));

        var service = new QueryExecutionService(
            Substitute.For<ILogger<QueryExecutionService>>(),
            executor);

        var request = new SubQueryRequest
        {
            SubQueryId = partialResult.SubQueryId.ToString("D"),
            ParentQueryId = partialResult.ParentQueryId.ToString("D"),
            Sql = "SELECT id FROM users",
            ShardIndex = partialResult.ShardIndex,
            TotalShards = 2,
            TimeoutMs = 1000
        };

        var responseWriter = new TestServerStreamWriter<PartialResultResponse>();
        var context = TestServerCallContext.Create();

        await service.ExecuteSubQuery(request, responseWriter, context);

        responseWriter.Messages.Should().HaveCount(3);

        var meta = responseWriter.Messages[0].Meta;
        meta.ExecutionMs.Should().Be(15);
        meta.RowCount.Should().Be(2);
        meta.Columns.Should().ContainSingle().Which.Name.Should().Be("id");

        responseWriter.Messages[1].Chunk.Rows.Should().ContainSingle().Which.Values.Should().ContainSingle().Which.Should().Be("1");
        responseWriter.Messages[2].Chunk.Rows.Should().ContainSingle().Which.Values.Should().ContainSingle().Which.Should().Be("2");
    }

    [Fact]
    public async Task ExecuteSubQuery_WritesError_WhenExecutorReturnsFailure()
    {
        var partialResult = PartialResult.Failure(
            Guid.NewGuid(),
            Guid.NewGuid(),
            shardIndex: 0,
            PartialResultStatus.Failed,
            "SQL execution failed");

        var executor = Substitute.For<ISubQueryExecutor>();
        executor.ExecuteAsync(Arg.Any<SubQuery>(), Arg.Any<CancellationToken>())
            .Returns(GetAsyncEnumerable(partialResult));

        var service = new QueryExecutionService(
            Substitute.For<ILogger<QueryExecutionService>>(),
            executor);

        var request = new SubQueryRequest
        {
            SubQueryId = partialResult.SubQueryId.ToString("D"),
            ParentQueryId = partialResult.ParentQueryId.ToString("D"),
            Sql = "SELECT id FROM users",
            ShardIndex = partialResult.ShardIndex,
            TotalShards = 1,
            TimeoutMs = 1000
        };

        var responseWriter = new TestServerStreamWriter<PartialResultResponse>();
        var context = TestServerCallContext.Create();

        await service.ExecuteSubQuery(request, responseWriter, context);

        responseWriter.Messages.Should().HaveCount(1);
        responseWriter.Messages[0].Error.Code.Should().Be("FAILED");
        responseWriter.Messages[0].Error.Message.Should().Be("SQL execution failed");
    }

    [Fact]
    public async Task ExecuteSubQuery_ThrowsInvalidArgument_WhenSubQueryIdIsNotGuid()
    {
        var executor = Substitute.For<ISubQueryExecutor>();
        var service = new QueryExecutionService(
            Substitute.For<ILogger<QueryExecutionService>>(),
            executor);

        var request = new SubQueryRequest
        {
            SubQueryId = "not-a-guid",
            ParentQueryId = Guid.NewGuid().ToString("D"),
            Sql = "SELECT id FROM users",
            ShardIndex = 0,
            TotalShards = 1,
            TimeoutMs = 1000
        };

        var responseWriter = new TestServerStreamWriter<PartialResultResponse>();
        var context = TestServerCallContext.Create();

        Func<Task> act = async () => await service.ExecuteSubQuery(request, responseWriter, context);

        await act.Should().ThrowAsync<RpcException>().Where(ex => ex.StatusCode == StatusCode.InvalidArgument);
    }

    private static async IAsyncEnumerable<PartialResult> GetAsyncEnumerable(PartialResult result)
    {
        yield return result;
        await Task.CompletedTask;
    }

    internal sealed class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public List<T> Messages { get; } = new();
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message)
        {
            return WriteAsync(message, CancellationToken.None);
        }

        public Task WriteAsync(T message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    internal sealed class TestServerCallContext : ServerCallContext
    {
        private TestServerCallContext() { }

        public static ServerCallContext Create() => new TestServerCallContext();

        protected override string MethodCore => "/QueryExecution/ExecuteSubQuery";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "127.0.0.1";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new AuthContext("test", new Dictionary<string, List<AuthProperty>>());
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
