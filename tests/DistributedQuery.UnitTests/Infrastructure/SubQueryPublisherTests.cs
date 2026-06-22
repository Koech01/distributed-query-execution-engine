using DistributedQuery.Core.Messages;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Messaging;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Infrastructure;

public class SubQueryPublisherTests
{
    [Fact]
    public async Task PublishAsync_PublishesSubQueryDispatchedMessage()
    {
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var publisher = new SubQueryPublisher(publishEndpoint, NullLogger<SubQueryPublisher>.Instance);
        var subQuery = SubQuery.Create(Guid.NewGuid(), "SELECT 1", "worker-01", 1, 4);

        await publisher.PublishAsync(subQuery, timeoutMs: 30000, CancellationToken.None);

        await publishEndpoint.Received(1).Publish(
            Arg.Is<SubQueryDispatched>(message =>
                message.SubQueryId == subQuery.SubQueryId &&
                message.ParentQueryId == subQuery.ParentQueryId &&
                message.ShardIndex == subQuery.ShardIndex &&
                message.TimeoutMs == 30000),
            Arg.Any<CancellationToken>());
    }
}
