using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Messages;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Messaging;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Infrastructure;

public class SubQueryConsumerTests
{
    [Fact]
    public async Task Consume_PublishesPartialResultReadyForSuccessfulExecution()
    {
        var executor = Substitute.For<ISubQueryExecutor>();
        var consumer = new SubQueryConsumer(executor, NullLogger<SubQueryConsumer>.Instance);
        var context = Substitute.For<ConsumeContext<SubQueryDispatched>>();
        var message = new SubQueryDispatched(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SELECT id FROM users",
            0,
            2,
            Array.Empty<QueryParameter>(),
            3000,
            null,
            null);

        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        executor.ExecuteAsync(Arg.Any<SubQuery>(), Arg.Any<CancellationToken>())
            .Returns(ReturnPartialResults(message));

        await consumer.Consume(context);

        await context.Received(1).Publish(
            Arg.Is<PartialResultReady>(ready =>
                ready.SubQueryId == message.SubQueryId &&
                ready.ParentQueryId == message.ParentQueryId &&
                ready.TotalShards == message.TotalShards &&
                ready.Success),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_PublishesFailureMessageWhenExecutorThrows()
    {
        var executor = Substitute.For<ISubQueryExecutor>();
        executor.ExecuteAsync(Arg.Any<SubQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("boom"));

        var consumer = new SubQueryConsumer(executor, NullLogger<SubQueryConsumer>.Instance);
        var context = Substitute.For<ConsumeContext<SubQueryDispatched>>();
        var message = new SubQueryDispatched(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SELECT 1",
            1,
            4,
            Array.Empty<QueryParameter>(),
            5000,
            null,
            null);

        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        await context.Received(1).Publish(
            Arg.Is<PartialResultReady>(ready => !ready.Success && ready.ErrorMessage != null),
            Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<PartialResult> ReturnPartialResults(SubQueryDispatched message)
    {
        yield return PartialResult.Success(
            message.SubQueryId,
            message.ParentQueryId,
            message.ShardIndex,
            new[] { new ColumnDescriptor("id", "int", false) },
            new[] { (IReadOnlyList<string>)new[] { "1" } },
            10);

        await Task.CompletedTask;
    }
}
