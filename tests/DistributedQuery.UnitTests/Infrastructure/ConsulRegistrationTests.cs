using Consul;
using DistributedQuery.Infrastructure.Discovery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DistributedQuery.UnitTests.Infrastructure;

public class ConsulRegistrationTests
{
    [Fact]
    public async Task StartAndStopAsync_RegisterAndDeregisterWorkerService()
    {
        var consulClient = Substitute.For<IConsulClient>();
        var agentEndpoint = Substitute.For<IAgentEndpoint>();
        consulClient.Agent.Returns(agentEndpoint);

        AgentServiceRegistration? capturedRegistration = null;
        agentEndpoint
            .ServiceRegister(Arg.Do<AgentServiceRegistration>(r => capturedRegistration = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new WriteResult()));
        agentEndpoint
            .ServiceDeregister(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new WriteResult()));

        var service = new ConsulRegistration(
            consulClient,
            Options.Create(new ConsulOptions { ServiceName = "distributed-query-worker" }),
            Options.Create(new WorkerRegistrationOptions
            {
                Enabled = true,
                NodeId = "worker-01",
                Address = "10.0.1.42",
                GrpcPort = 5100,
                HealthPort = 5101,
                ShardIndices = new[] { 0, 1, 2 },
                Version = "1.2.0"
            }),
            NullLogger<ConsulRegistration>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        capturedRegistration.Should().NotBeNull();
        capturedRegistration!.Meta["node_id"].Should().Be("worker-01");
        capturedRegistration.Meta["shards"].Should().Be("0,1,2");
        await agentEndpoint.Received(1).ServiceRegister(Arg.Any<AgentServiceRegistration>(), Arg.Any<CancellationToken>());
        await agentEndpoint.Received(1).ServiceDeregister(Arg.Is<string>(id => id.StartsWith("worker-01-")), Arg.Any<CancellationToken>());
    }
}
