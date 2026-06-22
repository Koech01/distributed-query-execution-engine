using Consul;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Infrastructure.Discovery;
using DistributedQuery.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Worker;

public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services, IConfiguration configuration)
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
            .AddOptions<WorkerOptions>()
            .Bind(configuration.GetSection(WorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                static options => options.ShardIndices.Count > 0,
                "Worker:ShardIndices must contain at least one shard index.")
            .ValidateOnStart();

        services
            .AddOptions<ConsulOptions>()
            .Bind(configuration.GetSection(ConsulOptions.SectionName))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.Address),
                "Consul:Address is required.");

        services.Configure<WorkerRegistrationOptions>(options =>
        {
            var worker = configuration.GetSection(WorkerOptions.SectionName).Get<WorkerOptions>()
                ?? new WorkerOptions();

            options.Enabled = worker.Consul.Enabled;
            options.NodeId = worker.NodeId;
            options.Address = worker.Address;
            options.GrpcPort = worker.GrpcPort;
            options.HealthPort = worker.HealthPort;
            options.ShardIndices = worker.ShardIndices;
            options.Version = worker.Version;
            options.HealthCheckIntervalSeconds = worker.Consul.HealthCheckIntervalSeconds;
            options.HealthCheckTimeoutSeconds = worker.Consul.HealthCheckTimeoutSeconds;
            options.DeregisterCriticalServiceAfterSeconds = worker.Consul.DeregisterCriticalServiceAfterSeconds;
        });

        services.AddSingleton<WorkerLifecycleState>();
        services.AddSingleton<ShardConnectionResolver>();
        services.AddSingleton<ISubQueryExecutor, ShardExecutor>();
        services.AddSingleton<WorkerHealthService>();
        services.AddHostedService<WorkerRegistration>();

        services.AddSingleton<IConsulClient>(serviceProvider =>
        {
            var consulOptions = serviceProvider.GetRequiredService<IOptions<ConsulOptions>>().Value;
            return new ConsulClient(config =>
            {
                config.Address = new Uri(consulOptions.Address);
                config.Token = consulOptions.Token;
            });
        });

        services.AddHostedService<ConsulRegistration>();

        return services;
    }
}
