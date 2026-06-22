using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Messages;
using DistributedQuery.Infrastructure.Messaging;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using System.Text.Json;

namespace DistributedQuery.UnitTests.Infrastructure;

public class PartialResultConsumerTests
{
    [Fact]
    public async Task Consume_PersistsResultToRedisRendezvousHash()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        var notifier = Substitute.For<IAsyncQueryCompletionNotifier>();
        multiplexer.GetDatabase().Returns(database);
        database.HashSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<RedisValue>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));
        database.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));
        database.HashLengthAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(2L));
        database.LockTakeAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CommandFlags>())
            .ReturnsForAnyArgs(Task.FromResult(true));

        var consumer = new PartialResultConsumer(
            multiplexer,
            notifier,
            Options.Create(new MessagingOptions { ResultRendezvousTtlHours = 24 }),
            NullLogger<PartialResultConsumer>.Instance);

        var context = Substitute.For<ConsumeContext<PartialResultReady>>();
        var message = new PartialResultReady(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            true,
            null,
            new[] { "id" },
            new[] { (IReadOnlyList<string>)new[] { "1" } },
            20,
            TotalShards: 2);
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        database.HashValuesAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(new RedisValue[]
            {
                JsonSerializer.Serialize(message),
                JsonSerializer.Serialize(message with { ShardIndex = 4 })
            }));

        await consumer.Consume(context);

        await database.Received(1).HashSetAsync(
            Arg.Is<RedisKey>(key => key.ToString() == $"query:{message.ParentQueryId:D}:partials"),
            Arg.Is<RedisValue>(value => value.ToString() == message.ShardIndex.ToString()),
            Arg.Any<RedisValue>(),
            When.Always,
            CommandFlags.None);
        await database.Received(1).KeyExpireAsync(
            Arg.Is<RedisKey>(key => key.ToString() == $"query:{message.ParentQueryId:D}:partials"),
            Arg.Any<TimeSpan?>(),
            ExpireWhen.Always,
            CommandFlags.None);
        await notifier.Received(1).NotifyCompletedAsync(
            Arg.Is<QueryCompleted>(completed => completed.QueryId == message.ParentQueryId && completed.Success),
            Arg.Any<CancellationToken>());
    }
}
