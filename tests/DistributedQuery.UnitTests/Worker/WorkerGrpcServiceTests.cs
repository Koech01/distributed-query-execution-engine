using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Grpc;
using DistributedQuery.UnitTests.Infrastructure;
using DistributedQuery.Worker.Services;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DistributedQuery.UnitTests.Worker;

public sealed class WorkerGrpcServiceTests
{
    [Fact]
    public async Task ExecuteSubQuery_MapsShardExecutionException_ToInternalStatus()
    {
        var subQueryId = Guid.NewGuid();
        var executor = Substitute.For<ISubQueryExecutor>();
        executor.ExecuteAsync(Arg.Any<SubQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ThrowingAsyncEnumerable(new ShardExecutionException(0, subQueryId, "SQL failed")));

        var loggerFactory = LoggerFactory.Create(static _ => { });
        var service = new WorkerGrpcService(loggerFactory, executor);
        var request = CreateRequest(subQueryId);
        var responseWriter = new QueryExecutionServiceTests.TestServerStreamWriter<PartialResultResponse>();
        var context = QueryExecutionServiceTests.TestServerCallContext.Create();

        Func<Task> act = async () => await service.ExecuteSubQuery(request, responseWriter, context);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.Internal && ex.Status.Detail.Contains("SQL failed"));
    }

    [Fact]
    public async Task ExecuteSubQuery_MapsQueryTimeoutException_ToDeadlineExceeded()
    {
        var subQueryId = Guid.NewGuid();
        var executor = Substitute.For<ISubQueryExecutor>();
        executor.ExecuteAsync(Arg.Any<SubQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ThrowingAsyncEnumerable(new QueryTimeoutException(subQueryId, TimeSpan.FromSeconds(1))));

        var loggerFactory = LoggerFactory.Create(static _ => { });
        var service = new WorkerGrpcService(loggerFactory, executor);
        var request = CreateRequest(subQueryId);
        var responseWriter = new QueryExecutionServiceTests.TestServerStreamWriter<PartialResultResponse>();
        var context = QueryExecutionServiceTests.TestServerCallContext.Create();

        Func<Task> act = async () => await service.ExecuteSubQuery(request, responseWriter, context);

        await act.Should().ThrowAsync<RpcException>()
            .Where(ex => ex.StatusCode == StatusCode.DeadlineExceeded);
    }

    private static SubQueryRequest CreateRequest(Guid subQueryId)
    {
        return new SubQueryRequest
        {
            SubQueryId = subQueryId.ToString("D"),
            ParentQueryId = Guid.NewGuid().ToString("D"),
            Sql = "SELECT 1",
            ShardIndex = 0,
            TotalShards = 1,
            TimeoutMs = 1000
        };
    }

    private sealed class ThrowingAsyncEnumerable : IAsyncEnumerable<PartialResult>
    {
        private readonly Exception _exception;

        public ThrowingAsyncEnumerable(Exception exception)
        {
            _exception = exception;
        }

        public IAsyncEnumerator<PartialResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}
