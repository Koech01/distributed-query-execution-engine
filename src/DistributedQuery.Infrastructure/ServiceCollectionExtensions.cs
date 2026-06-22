using Consul;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Infrastructure.Auth;
using DistributedQuery.Infrastructure.Caching;
using DistributedQuery.Infrastructure.Coordinator;
using DistributedQuery.Infrastructure.Discovery;
using DistributedQuery.Infrastructure.Grpc;
using DistributedQuery.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DistributedQuery.Infrastructure;

public enum InfrastructureHostRole
{
    Api,
    Coordinator,
    Worker,
    Full
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        InfrastructureHostRole hostRole = InfrastructureHostRole.Full)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Redis:ConnectionString is required.");

        services
            .AddOptions<ConsulOptions>()
            .Bind(configuration.GetSection(ConsulOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Address), "Consul:Address is required.");

        services
            .AddOptions<MessagingOptions>()
            .Bind(configuration.GetSection(MessagingOptions.SectionName));

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMq:Host is required.");

        services
            .AddOptions<WorkerRegistrationOptions>()
            .Bind(configuration.GetSection(WorkerRegistrationOptions.SectionName));

        services.AddMemoryCache();

        services.AddSingleton<IConnectionMultiplexer>(static serviceProvider =>
        {
            var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
        });

        services.AddSingleton<IConsulClient>(serviceProvider =>
        {
            var consulOptions = serviceProvider.GetRequiredService<IOptions<ConsulOptions>>().Value;
            return new ConsulClient(config =>
            {
                config.Address = new Uri(consulOptions.Address);
                config.Token = consulOptions.Token;
            });
        });

        services.AddSingleton<IQueryCache, RedisQueryCache>();
        services.AddSingleton<IQueryCacheAdmin>(static serviceProvider =>
            (IQueryCacheAdmin)serviceProvider.GetRequiredService<IQueryCache>());

        if (hostRole is InfrastructureHostRole.Api or InfrastructureHostRole.Full)
        {
            services.AddDistributedQueryAuth(configuration, hostRole);
        }

        if (hostRole is InfrastructureHostRole.Coordinator or InfrastructureHostRole.Worker or InfrastructureHostRole.Full)
        {
            services.AddSingleton<INodeRegistry, ConsulNodeRegistry>();
            services.AddHostedService<ConsulRegistration>();
        }

        if (hostRole is InfrastructureHostRole.Coordinator or InfrastructureHostRole.Worker or InfrastructureHostRole.Full)
        {
            services.AddTransient<IWorkerClient, WorkerGrpcClient>();
            services.AddTransient<SubQueryPublisher>();
            services.AddTransient<IAsyncQueryCompletionNotifier, MassTransitQueryCompletionNotifier>();
        }

        services
            .AddOptions<CoordinatorClientOptions>()
            .Bind(configuration.GetSection(CoordinatorClientOptions.SectionName))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.BaseUrl),
                "CoordinatorClient:BaseUrl is required.");

        services.AddHttpClient<IQueryCoordinatorClient, CoordinatorHttpClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<CoordinatorClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs);
        });

        if (hostRole is InfrastructureHostRole.Coordinator or InfrastructureHostRole.Worker or InfrastructureHostRole.Full)
        {
            services.AddMassTransit(configurator =>
            {
                if (hostRole is InfrastructureHostRole.Worker or InfrastructureHostRole.Full)
                {
                    configurator.AddConsumer<SubQueryConsumer>();
                }

                if (hostRole is InfrastructureHostRole.Coordinator or InfrastructureHostRole.Full)
                {
                    configurator.AddConsumer<PartialResultConsumer>();
                }

                configurator.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitMqOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                    var messagingOptions = context.GetRequiredService<IOptions<MessagingOptions>>().Value;

                    cfg.Host(rabbitMqOptions.Host, rabbitMqOptions.VirtualHost, host =>
                    {
                        host.Username(rabbitMqOptions.Username);
                        host.Password(rabbitMqOptions.Password);
                    });

                    if (messagingOptions.EnableWorkerConsumer &&
                        hostRole is InfrastructureHostRole.Worker or InfrastructureHostRole.Full)
                    {
                        foreach (var shardIndex in messagingOptions.WorkerShardIndices.Distinct())
                        {
                            cfg.ReceiveEndpoint($"worker.shard.{shardIndex}", endpoint =>
                            {
                                endpoint.ConfigureConsumer<SubQueryConsumer>(context);
                                endpoint.PrefetchCount = (ushort)Math.Max(1, messagingOptions.WorkerPrefetchCount);
                            });
                        }
                    }

                    if (messagingOptions.EnableCoordinatorConsumer &&
                        hostRole is InfrastructureHostRole.Coordinator or InfrastructureHostRole.Full)
                    {
                        cfg.ReceiveEndpoint("coordinator.results", endpoint =>
                        {
                            endpoint.ConfigureConsumer<PartialResultConsumer>(context);
                            endpoint.PrefetchCount = (ushort)Math.Max(1, messagingOptions.CoordinatorPrefetchCount);
                        });
                    }
                });
            });
        }

        return services;
    }
}
