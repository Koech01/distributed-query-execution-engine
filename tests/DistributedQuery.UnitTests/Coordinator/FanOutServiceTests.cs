using DistributedQuery.Coordinator;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DistributedQuery.UnitTests.Coordinator;

public class FanOutServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsDegradedResult_WhenOneWorkerFails()
    {
        var workerClient = Substitute.For<IWorkerClient>();
        var options = Options.Create(new CoordinatorOptions
        {
            FanOut = new FanOutOptions
            {
                MaxConcurrentWorkerCalls = 10,
                PerWorkerTimeoutMs = 500
            },
            Resilience = new ResilienceOptions
            {
                RetryCount = 0,
                RetryBaseDelayMs = 1,
                CircuitBreakerFailureThreshold = 2,
                CircuitBreakerSamplingDurationSeconds = 30,
                CircuitBreakerBreakDurationSeconds = 1
            }
        });

        var queryId = Guid.NewGuid();
        var successSubQuery = SubQuery.Create(queryId, "SELECT 1", "worker-ok", 0, 2);
        var failedSubQuery = SubQuery.Create(queryId, "SELECT 1", "worker-fail", 1, 2);

        workerClient.ExecuteAsync(Arg.Any<SubQuery>(), Arg.Any<NodeInfo>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var subQuery = callInfo.ArgAt<SubQuery>(0);
                if (subQuery.SubQueryId == failedSubQuery.SubQueryId)
                {
                    throw new InvalidOperationException("worker down");
                }

                return SuccessStream(subQuery);
            });

        var service = new FanOutService(workerClient, options, Substitute.For<ILogger<FanOutService>>());
        var assignments = new[]
        {
            (successSubQuery, new NodeInfo("worker-ok", "localhost", 5100, new[] { 0 }, "1.0")),
            (failedSubQuery, new NodeInfo("worker-fail", "localhost", 5101, new[] { 1 }, "1.0"))
        };

        var results = await service.ExecuteAsync(assignments, CancellationToken.None);

        results.Should().HaveCount(2);
        results.Should().ContainSingle(r => r.SubQueryId == failedSubQuery.SubQueryId && r.Status == PartialResultStatus.Failed);
        results.Should().Contain(r =>
            r.Status == PartialResultStatus.Success ||
            r.Status == PartialResultStatus.Degraded ||
            r.Status == PartialResultStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_MapsCancellationToTimedOutPartialResult()
    {
        var workerClient = Substitute.For<IWorkerClient>();
        var options = Options.Create(new CoordinatorOptions
        {
            FanOut = new FanOutOptions
            {
                MaxConcurrentWorkerCalls = 5,
                PerWorkerTimeoutMs = 100
            },
            Resilience = new ResilienceOptions
            {
                RetryCount = 0,
                RetryBaseDelayMs = 1,
                CircuitBreakerFailureThreshold = 2,
                CircuitBreakerSamplingDurationSeconds = 30,
                CircuitBreakerBreakDurationSeconds = 1
            }
        });

        var queryId = Guid.NewGuid();
        var subQuery = SubQuery.Create(queryId, "SELECT 1", "worker-timeout", 0, 1);
        workerClient.ExecuteAsync(subQuery, Arg.Any<NodeInfo>(), Arg.Any<CancellationToken>())
            .Returns(CancelledStream());

        var service = new FanOutService(workerClient, options, Substitute.For<ILogger<FanOutService>>());
        var assignment = (subQuery, new NodeInfo("worker-timeout", "localhost", 5100, new[] { 0 }, "1.0"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var results = await service.ExecuteAsync(new[] { assignment }, cts.Token);

        results.Should().ContainSingle();
        results[0].Status.Should().Be(PartialResultStatus.TimedOut);
    }

    private static async IAsyncEnumerable<PartialResult> SuccessStream(SubQuery subQuery)
    {
        await Task.Yield();
        yield return PartialResult.Success(
            subQuery.SubQueryId,
            subQuery.ParentQueryId,
            subQuery.ShardIndex,
            [new ColumnDescriptor("value", "int", false)],
            [["1"]],
            10);
    }

    private static async IAsyncEnumerable<PartialResult> CancelledStream()
    {
        await Task.Yield();
        if (DateTime.UtcNow > DateTime.MinValue)
        {
            throw new OperationCanceledException();
        }

        yield return PartialResult.Success(Guid.NewGuid(), Guid.NewGuid(), 0, [], [], 0);
    }
}