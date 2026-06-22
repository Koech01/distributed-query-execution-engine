namespace DistributedQuery.Core.Exceptions;

public sealed class InsufficientNodesException : Exception
{
    public int RequiredShards { get; }
    public int AvailableNodes { get; }

    public InsufficientNodesException(int requiredShards, int availableNodes)
        : base($"Query requires {requiredShards} shards but only {availableNodes} healthy nodes are available.")
    {
        RequiredShards = requiredShards;
        AvailableNodes = availableNodes;
    }
}
