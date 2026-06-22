using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreColumnDescriptor = DistributedQuery.Core.Models.ColumnDescriptor;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Grpc;
using ProtoColumnDescriptor = DistributedQuery.Infrastructure.Grpc.ColumnDescriptor;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DistributedQuery.UnitTests.Infrastructure;

public class WorkerGrpcClientTests
{
    [Fact]
    public async Task ExecuteAsync_YieldsEachChunkAsSeparatePartialResult()
    {
        var client = CreateClientWithResponses(
            new PartialResultResponse
            {
                Meta = new PartialResultMeta
                {
                    ExecutionMs = 5,
                    RowCount = 2,
                    Columns = { new ProtoColumnDescriptor { Name = "id", DataType = "int", Nullable = false } }
                }
            },
            new PartialResultResponse
            {
                Chunk = new PartialResultChunk
                {
                    Rows = { new ResultRow { Values = { "1" } } }
                }
            },
            new PartialResultResponse
            {
                Chunk = new PartialResultChunk
                {
                    Rows = { new ResultRow { Values = { "2" } } }
                }
            });

        var grpcClient = new WorkerGrpcClient(
            Substitute.For<ILogger<WorkerGrpcClient>>(),
            clientFactory: _ => client);

        var subQuery = SubQuery.Create(Guid.NewGuid(), "SELECT id FROM users", "node-1", 0, 4);
        var node = new NodeInfo("node-1", "localhost", 5100, new[] { 0 }, "1.0");

        var results = new List<PartialResult>();
        await foreach (var partial in grpcClient.ExecuteAsync(subQuery, node))
        {
            results.Add(partial);
        }

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Status.Should().Be(PartialResultStatus.Success));
        results.SelectMany(r => r.Rows).Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessWhenWorkerReturnsMetaAndChunk()
    {
        var client = CreateClientWithResponses(
            new PartialResultResponse
            {
                Meta = new PartialResultMeta
                {
                    ExecutionMs = 12,
                    RowCount = 1,
                    Columns = { new ProtoColumnDescriptor { Name = "id", DataType = "int", Nullable = false } }
                }
            },
            new PartialResultResponse
            {
                Chunk = new PartialResultChunk
                {
                    Rows = { new ResultRow { Values = { "1" } } }
                }
            });

        var grpcClient = new WorkerGrpcClient(
            Substitute.For<ILogger<WorkerGrpcClient>>(),
            clientFactory: _ => client);

        var subQuery = SubQuery.Create(Guid.NewGuid(), "SELECT id FROM users", "node-1", 0, 1);
        var node = new NodeInfo("node-1", "localhost", 5100, new[] { 0 }, "1.0");

        var results = new List<PartialResult>();
        await foreach (var partial in grpcClient.ExecuteAsync(subQuery, node))
        {
            results.Add(partial);
        }

        results.Should().ContainSingle();
        var result = results.Single();
        result.Status.Should().Be(PartialResultStatus.Success);
        result.Columns.Should().ContainSingle().Which.Name.Should().Be("id");
        result.Rows.Should().ContainSingle().Which.Should().ContainSingle().Which.Should().Be("1");
        result.ExecutionMs.Should().Be(12);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDegradedWhenWorkerReturnsErrorAfterRows()
    {
        var client = CreateClientWithResponses(
            new PartialResultResponse
            {
                Meta = new PartialResultMeta
                {
                    ExecutionMs = 20,
                    RowCount = 2,
                    Columns = { new ProtoColumnDescriptor { Name = "value", DataType = "nvarchar", Nullable = true } }
                }
            },
            new PartialResultResponse
            {
                Chunk = new PartialResultChunk
                {
                    Rows = { new ResultRow { Values = { "x" } } }
                }
            },
            new PartialResultResponse
            {
                Error = new PartialResultError
                {
                    Code = "INTERNAL",
                    Message = "Worker execution failed"
                }
            });

        var grpcClient = new WorkerGrpcClient(
            Substitute.For<ILogger<WorkerGrpcClient>>(),
            clientFactory: _ => client);

        var subQuery = SubQuery.Create(Guid.NewGuid(), "SELECT value FROM orders", "node-2", 1, 2);
        var node = new NodeInfo("node-2", "localhost", 5200, new[] { 1 }, "1.0");

        var results = new List<PartialResult>();
        await foreach (var partial in grpcClient.ExecuteAsync(subQuery, node))
        {
            results.Add(partial);
        }

        results.Should().HaveCount(2);
        results[0].Status.Should().Be(PartialResultStatus.Success);
        results[0].Rows.Should().ContainSingle();

        var result = results[1];
        result.Status.Should().Be(PartialResultStatus.Degraded);
        result.ErrorMessage.Should().Be("Worker execution failed");
        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTimedOutWhenRpcDeadlineExceeded()
    {
        var call = new AsyncServerStreamingCall<PartialResultResponse>(
            new ThrowingAsyncStreamReader<PartialResultResponse>(
                new RpcException(new Status(StatusCode.DeadlineExceeded, "deadline exceeded"))),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

        var client = Substitute.For<QueryExecution.QueryExecutionClient>(Substitute.For<CallInvoker>());
        client.ExecuteSubQuery(Arg.Any<SubQueryRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(call);

        var grpcClient = new WorkerGrpcClient(
            Substitute.For<ILogger<WorkerGrpcClient>>(),
            clientFactory: _ => client);

        var subQuery = SubQuery.Create(Guid.NewGuid(), "SELECT id FROM logs", "node-3", 2, 3);
        var node = new NodeInfo("node-3", "localhost", 5300, new[] { 2 }, "1.0");

        var results = new List<PartialResult>();
        await foreach (var partial in grpcClient.ExecuteAsync(subQuery, node))
        {
            results.Add(partial);
        }

        results.Should().ContainSingle();
        var result = results.Single();
        result.Status.Should().Be(PartialResultStatus.TimedOut);
        result.ErrorMessage.Should().Contain("deadline exceeded");
        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_SendsTimeoutAndGrpcDeadline()
    {
        SubQueryRequest? capturedRequest = null;
        DateTime? capturedDeadline = null;
        var call = new AsyncServerStreamingCall<PartialResultResponse>(
            new TestAsyncStreamReader<PartialResultResponse>([
                new PartialResultResponse
                {
                    Meta = new PartialResultMeta
                    {
                        ExecutionMs = 1,
                        RowCount = 0,
                        Columns = { new ProtoColumnDescriptor { Name = "id", DataType = "int", Nullable = false } }
                    }
                }
            ]),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

        var client = Substitute.For<QueryExecution.QueryExecutionClient>(Substitute.For<CallInvoker>());
        client.ExecuteSubQuery(
                Arg.Do<SubQueryRequest>(request => capturedRequest = request),
                Arg.Any<Metadata>(),
                Arg.Do<DateTime?>(deadline => capturedDeadline = deadline),
                Arg.Any<CancellationToken>())
            .Returns(call);

        var grpcClient = new WorkerGrpcClient(
            Substitute.For<ILogger<WorkerGrpcClient>>(),
            clientFactory: _ => client);

        var subQuery = SubQuery.Create(Guid.NewGuid(), "SELECT id FROM logs", "node-3", 2, 3, timeoutMs: 5000);
        var node = new NodeInfo("node-3", "localhost", 5300, new[] { 2 }, "1.0");

        await foreach (var _ in grpcClient.ExecuteAsync(subQuery, node))
        {
        }

        capturedRequest.Should().NotBeNull();
        capturedRequest!.TimeoutMs.Should().Be(5000);
        capturedDeadline.Should().NotBeNull();
        capturedDeadline.Should().BeAfter(DateTime.UtcNow);
    }

    private static QueryExecution.QueryExecutionClient CreateClientWithResponses(params PartialResultResponse[] responses)
    {
        var call = new AsyncServerStreamingCall<PartialResultResponse>(
            new TestAsyncStreamReader<PartialResultResponse>(responses),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

        var client = Substitute.For<QueryExecution.QueryExecutionClient>(Substitute.For<CallInvoker>());
        client.ExecuteSubQuery(Arg.Any<SubQueryRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(call);

        return client;
    }

    private sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly IEnumerator<T> _enumerator;

        public TestAsyncStreamReader(IEnumerable<T> items)
        {
            _enumerator = items.GetEnumerator();
        }

        public T Current => _enumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(_enumerator.MoveNext());
        }
    }

    private sealed class ThrowingAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly RpcException _exception;

        public ThrowingAsyncStreamReader(RpcException exception)
        {
            _exception = exception;
        }

        public T Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }
}
