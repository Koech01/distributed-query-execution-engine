using Consul;
using DistributedQuery.Infrastructure.Discovery;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using CoreNodeInfo = DistributedQuery.Core.Models.NodeInfo;

namespace DistributedQuery.UnitTests.Infrastructure;

public class ConsulNodeRegistryTests
{
    [Fact]
    public async Task GetHealthyNodesAsync_UsesPassingOnlyAndMapsNodes()
    {
        var consulClient = Substitute.For<IConsulClient>();
        var healthEndpoint = Substitute.For<IHealthEndpoint>();
        consulClient.Health.Returns(healthEndpoint);

        healthEndpoint
            .Service(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult<ServiceEntry[]>
            {
                Response =
                [
                    new ServiceEntry
                    {
                        Node = new Consul.Node { Address = "10.0.0.2" },
                        Service = new AgentService
                        {
                            ID = "worker-1",
                            Address = "10.0.0.2",
                            Meta = new Dictionary<string, string>
                            {
                                ["node_id"] = "worker-1",
                                ["grpc_port"] = "5100",
                                ["version"] = "1.0.0",
                                ["shards"] = "0,1"
                            }
                        }
                    }
                ]
            });

        var registry = new ConsulNodeRegistry(
            consulClient,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new ConsulOptions()),
            NullLogger<ConsulNodeRegistry>.Instance);

        var nodes = await registry.GetHealthyNodesAsync();

        nodes.Should().ContainSingle();
        nodes[0].Should().BeEquivalentTo(new CoreNodeInfo("worker-1", "10.0.0.2", 5100, new[] { 0, 1 }, "1.0.0"));
        await healthEndpoint.Received(1)
            .Service(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                true,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHealthyNodesAsync_UsesInMemoryCacheForFiveSecondWindow()
    {
        var consulClient = Substitute.For<IConsulClient>();
        var healthEndpoint = Substitute.For<IHealthEndpoint>();
        consulClient.Health.Returns(healthEndpoint);

        healthEndpoint
            .Service(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult<ServiceEntry[]>
            {
                Response = Array.Empty<ServiceEntry>()
            });

        var registry = new ConsulNodeRegistry(
            consulClient,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new ConsulOptions { NodeCacheTtlSeconds = 5 }),
            NullLogger<ConsulNodeRegistry>.Instance);

        await registry.GetHealthyNodesAsync();
        await registry.GetHealthyNodesAsync();

        await healthEndpoint.Received(1)
            .Service(Arg.Any<string>(), Arg.Any<string?>(), true, Arg.Any<CancellationToken>());
    }
}
