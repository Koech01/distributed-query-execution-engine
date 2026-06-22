using System.Diagnostics;
using Consul;
using DistributedQuery.Core.Interfaces;
using CoreNodeInfo = DistributedQuery.Core.Models.NodeInfo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Infrastructure.Discovery;

public sealed class ConsulNodeRegistry : INodeRegistry
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Discovery.ConsulNodeRegistry");
    private const string CacheKey = "consul:healthy-nodes";

    private readonly IConsulClient _consulClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ConsulOptions _options;
    private readonly ILogger<ConsulNodeRegistry> _logger;

    public ConsulNodeRegistry(
        IConsulClient consulClient,
        IMemoryCache memoryCache,
        IOptions<ConsulOptions> options,
        ILogger<ConsulNodeRegistry> logger)
    {
        _consulClient = consulClient ?? throw new ArgumentNullException(nameof(consulClient));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<CoreNodeInfo>> GetHealthyNodesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ConsulNodeRegistry.GetHealthyNodes", ActivityKind.Client);
        activity?.SetTag("consul.service_name", _options.ServiceName);

        if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<CoreNodeInfo>? cachedNodes) && cachedNodes is not null)
        {
            activity?.SetTag("cache.hit", true);
            _logger.LogDebug("Returning {NodeCount} healthy nodes from in-memory cache", cachedNodes.Count);
            return cachedNodes;
        }

        activity?.SetTag("cache.hit", false);
        var result = await _consulClient.Health.Service(
            _options.ServiceName,
            tag: null,
            passingOnly: true,
            cancellationToken).ConfigureAwait(false);

        var nodes = (result.Response ?? Array.Empty<ServiceEntry>())
            .Select(MapToNodeInfo)
            .Where(node => node is not null)
            .Select(node => node!)
            .ToArray();

        _memoryCache.Set(CacheKey, nodes, TimeSpan.FromSeconds(Math.Max(1, _options.NodeCacheTtlSeconds)));
        _logger.LogInformation("Resolved {NodeCount} healthy nodes from Consul", nodes.Length);
        return nodes;
    }

    public Task RegisterAsync(CoreNodeInfo node, CancellationToken cancellationToken = default)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        using var activity = ActivitySource.StartActivity("ConsulNodeRegistry.Register", ActivityKind.Client);
        activity?.SetTag("worker.node_id", node.NodeId);

        var registration = BuildRegistration(node);
        return _consulClient.Agent.ServiceRegister(registration, cancellationToken);
    }

    public Task DeregisterAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id is required.", nameof(nodeId));
        }

        using var activity = ActivitySource.StartActivity("ConsulNodeRegistry.Deregister", ActivityKind.Client);
        activity?.SetTag("worker.node_id", nodeId);
        return _consulClient.Agent.ServiceDeregister(nodeId, cancellationToken);
    }

    private CoreNodeInfo? MapToNodeInfo(ServiceEntry serviceEntry)
    {
        var meta = serviceEntry.Service.Meta;

        if (meta is null ||
            !meta.TryGetValue("node_id", out var nodeId) ||
            !meta.TryGetValue("grpc_port", out var grpcPortRaw) ||
            !meta.TryGetValue("version", out var version))
        {
            _logger.LogWarning(
                "Skipping Consul service entry {ServiceId} due to missing required metadata",
                serviceEntry.Service.ID);
            return null;
        }

        if (!int.TryParse(grpcPortRaw, out var grpcPort))
        {
            _logger.LogWarning(
                "Skipping Consul service entry {ServiceId} due to invalid grpc_port metadata value {GrpcPort}",
                serviceEntry.Service.ID,
                grpcPortRaw);
            return null;
        }

        var healthPort = 0;
        if (meta.TryGetValue("health_port", out var healthPortRaw) &&
            !int.TryParse(healthPortRaw, out healthPort))
        {
            _logger.LogWarning(
                "Consul service entry {ServiceId} has invalid health_port metadata value {HealthPort}",
                serviceEntry.Service.ID,
                healthPortRaw);
            healthPort = 0;
        }

        var shards = Array.Empty<int>();
        if (meta.TryGetValue("shards", out var shardsRaw) && !string.IsNullOrWhiteSpace(shardsRaw))
        {
            shards = shardsRaw
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(static shard => int.TryParse(shard, out var value) ? value : -1)
                .Where(static shard => shard >= 0)
                .ToArray();
        }

        var address = string.IsNullOrWhiteSpace(serviceEntry.Service.Address)
            ? serviceEntry.Node.Address
            : serviceEntry.Service.Address;

        return new CoreNodeInfo(nodeId, address, grpcPort, shards, version, healthPort);
    }

    private AgentServiceRegistration BuildRegistration(CoreNodeInfo node)
    {
        return new AgentServiceRegistration
        {
            ID = node.NodeId,
            Name = _options.ServiceName,
            Address = node.Address,
            Port = node.GrpcPort,
            Tags = new[] { "grpc", node.Version },
            Meta = new Dictionary<string, string>
            {
                ["node_id"] = node.NodeId,
                ["grpc_port"] = node.GrpcPort.ToString(),
                ["health_port"] = node.ResolvedHealthPort.ToString(),
                ["version"] = node.Version,
                ["shards"] = string.Join(",", node.Shards)
            }
        };
    }
}
