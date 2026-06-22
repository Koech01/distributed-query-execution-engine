using System.Diagnostics;
using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Infrastructure.Discovery;

public sealed class ConsulRegistration : IHostedService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Discovery.ConsulRegistration");

    private readonly IConsulClient _consulClient;
    private readonly ConsulOptions _consulOptions;
    private readonly WorkerRegistrationOptions _workerOptions;
    private readonly ILogger<ConsulRegistration> _logger;
    private string? _serviceId;

    public ConsulRegistration(
        IConsulClient consulClient,
        IOptions<ConsulOptions> consulOptions,
        IOptions<WorkerRegistrationOptions> workerOptions,
        ILogger<ConsulRegistration> logger)
    {
        _consulClient = consulClient ?? throw new ArgumentNullException(nameof(consulClient));
        _consulOptions = consulOptions?.Value ?? throw new ArgumentNullException(nameof(consulOptions));
        _workerOptions = workerOptions?.Value ?? throw new ArgumentNullException(nameof(workerOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_workerOptions.Enabled)
        {
            _logger.LogInformation("Consul registration is disabled");
            return;
        }

        EnsureRegistrationInputs();

        using var activity = ActivitySource.StartActivity("ConsulRegistration.Start", ActivityKind.Client);
        _serviceId = $"{_workerOptions.NodeId}-{Environment.MachineName}";
        activity?.SetTag("worker.node_id", _workerOptions.NodeId);
        activity?.SetTag("consul.service_id", _serviceId);

        var registration = new AgentServiceRegistration
        {
            ID = _serviceId,
            Name = _consulOptions.ServiceName,
            Address = _workerOptions.Address,
            Port = _workerOptions.GrpcPort,
            Tags = new[] { "grpc", _workerOptions.Version },
            Meta = new Dictionary<string, string>
            {
                ["node_id"] = _workerOptions.NodeId,
                ["grpc_port"] = _workerOptions.GrpcPort.ToString(),
                ["health_port"] = _workerOptions.HealthPort.ToString(),
                ["version"] = _workerOptions.Version,
                ["shards"] = string.Join(",", _workerOptions.ShardIndices)
            },
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{_workerOptions.Address}:{_workerOptions.HealthPort}/health/ready",
                Interval = TimeSpan.FromSeconds(Math.Max(1, _workerOptions.HealthCheckIntervalSeconds)),
                Timeout = TimeSpan.FromSeconds(Math.Max(1, _workerOptions.HealthCheckTimeoutSeconds)),
                DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(Math.Max(5, _workerOptions.DeregisterCriticalServiceAfterSeconds))
            }
        };

        await _consulClient.Agent.ServiceRegister(registration, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Registered worker {NodeId} in Consul with service id {ServiceId}",
            _workerOptions.NodeId,
            _serviceId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_workerOptions.Enabled || string.IsNullOrWhiteSpace(_serviceId))
        {
            return;
        }

        using var activity = ActivitySource.StartActivity("ConsulRegistration.Stop", ActivityKind.Client);
        activity?.SetTag("consul.service_id", _serviceId);

        await _consulClient.Agent.ServiceDeregister(_serviceId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Deregistered worker {NodeId} from Consul service id {ServiceId}",
            _workerOptions.NodeId,
            _serviceId);
    }

    private void EnsureRegistrationInputs()
    {
        if (string.IsNullOrWhiteSpace(_workerOptions.NodeId))
        {
            throw new InvalidOperationException("WorkerRegistration:NodeId must be configured when Consul registration is enabled.");
        }

        if (string.IsNullOrWhiteSpace(_workerOptions.Address))
        {
            throw new InvalidOperationException("WorkerRegistration:Address must be configured when Consul registration is enabled.");
        }
    }
}
