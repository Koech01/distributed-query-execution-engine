using System.Diagnostics;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Messages;
using DistributedQuery.Infrastructure.Messaging;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace DistributedQuery.UnitTests.Infrastructure;

public class PartialResultTracePropagationTests
{
    [Fact]
    public async Task PartialResultConsumer_UsesTraceParentFromMessage()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var parentTraceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        var traceParent = $"00-{parentTraceId}-{parentSpanId}-01";

        var message = new PartialResultReady(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ShardIndex: 1,
            Success: true,
            ErrorMessage: null,
            ColumnNames: ["id"],
            Rows: [new[] { "1" }],
            ExecutionMs: 5,
            TraceParent: traceParent,
            TraceState: null);

        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        multiplexer.GetDatabase().Returns(database);
        database.HashSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<RedisValue>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));
        database.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));
        database.HashLengthAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(1L));

        var consumer = new PartialResultConsumer(
            multiplexer,
            Substitute.For<IAsyncQueryCompletionNotifier>(),
            Options.Create(new MessagingOptions { ResultRendezvousTtlHours = 1 }),
            NullLogger<PartialResultConsumer>.Instance);

        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "DistributedQuery.Infrastructure.Messaging.PartialResultConsumer",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                return ActivitySamplingResult.AllData;
            },
            ActivityStarted = _ => { },
            ActivityStopped = activity =>
            {
                if (activity.GetTagItem("query.id") as string == message.ParentQueryId.ToString())
                {
                    observed = activity;
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        var context = Substitute.For<ConsumeContext<PartialResultReady>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        observed.Should().NotBeNull();
        observed!.TraceId.Should().Be(parentTraceId);
        observed.ParentSpanId.Should().Be(parentSpanId);
    }
}
