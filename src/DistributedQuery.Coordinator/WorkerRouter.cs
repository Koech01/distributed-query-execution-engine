using System.Collections.Concurrent;
using System.Diagnostics;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Coordinator;

public sealed class WorkerRouter
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Coordinator");
    private readonly ConcurrentDictionary<int, int> _roundRobinCursors = new();
    private readonly ILogger<WorkerRouter> _logger;

    public WorkerRouter(ILogger<WorkerRouter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<(SubQuery SubQuery, NodeInfo Node)> Route(
        IReadOnlyList<SubQuery> subQueries,
        IReadOnlyList<NodeInfo> healthyNodes)
    {
        ArgumentNullException.ThrowIfNull(subQueries);
        ArgumentNullException.ThrowIfNull(healthyNodes);

        using var activity = ActivitySource.StartActivity("coordinator.route.workers", ActivityKind.Internal);
        activity?.SetTag("fanout.subquery_count", subQueries.Count);
        activity?.SetTag("nodes.healthy_count", healthyNodes.Count);

        var assignments = new List<(SubQuery, NodeInfo)>(subQueries.Count);

        foreach (var subQuery in subQueries)
        {
            var candidates = healthyNodes
                .Where(node => node.Shards.Contains(subQuery.ShardIndex))
                .OrderBy(node => node.NodeId, StringComparer.Ordinal)
                .ToList();

            if (candidates.Count == 0)
            {
                _logger.LogError(
                    "No healthy worker owns shard {ShardIndex} for sub-query {SubQueryId}",
                    subQuery.ShardIndex,
                    subQuery.SubQueryId);
                throw new InsufficientNodesException(requiredShards: subQueries.Count, availableNodes: healthyNodes.Count);
            }

            var selectedNode = SelectRoundRobin(subQuery.ShardIndex, candidates);
            assignments.Add((subQuery, selectedNode));
        }

        return assignments;
    }

    private NodeInfo SelectRoundRobin(int shardIndex, IReadOnlyList<NodeInfo> candidates)
    {
        var selectedIndex = _roundRobinCursors.AddOrUpdate(
            shardIndex,
            _ => 0,
            (_, current) => (current + 1) % candidates.Count);

        return candidates[selectedIndex];
    }
}
