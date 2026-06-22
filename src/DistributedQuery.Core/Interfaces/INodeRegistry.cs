using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface INodeRegistry
{
    Task<IReadOnlyList<NodeInfo>> GetHealthyNodesAsync(CancellationToken cancellationToken = default);
    Task RegisterAsync(NodeInfo node, CancellationToken cancellationToken = default);
    Task DeregisterAsync(string nodeId, CancellationToken cancellationToken = default);
}
