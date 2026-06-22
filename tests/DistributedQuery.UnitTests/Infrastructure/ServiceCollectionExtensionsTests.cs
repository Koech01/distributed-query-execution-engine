using DistributedQuery.Core.Interfaces;
using DistributedQuery.Infrastructure;
using DistributedQuery.Infrastructure.Caching;
using DistributedQuery.Infrastructure.Discovery;
using DistributedQuery.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.Infrastructure;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_RegistersInfrastructureServicesAndOptions()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false",
                ["Redis:InstanceName"] = "dqee:",
                ["Consul:Address"] = "http://localhost:8500",
                ["RabbitMq:Host"] = "localhost",
                ["Messaging:EnableWorkerConsumer"] = "true",
                ["Messaging:WorkerShardIndices:0"] = "0",
                ["WorkerRegistration:Enabled"] = "false"
            })
            .Build();

        services.AddLogging();
        services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IQueryCache>().Should().BeOfType<RedisQueryCache>();
        provider.GetRequiredService<INodeRegistry>().Should().BeOfType<ConsulNodeRegistry>();
        provider.GetRequiredService<SubQueryPublisher>().Should().NotBeNull();

        var redisOptions = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
        redisOptions.ConnectionString.Should().Be("localhost:6379,abortConnect=false");
    }
}
