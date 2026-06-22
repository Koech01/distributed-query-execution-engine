namespace DistributedQuery.Core.Models;

public sealed record NodeInfo(
    string NodeId,
    string Address,
    int GrpcPort,
    IReadOnlyList<int> Shards,
    string Version,
    int HealthPort = 0)
{
    public int ResolvedHealthPort => HealthPort > 0 ? HealthPort : GrpcPort + 1;

    public bool Equals(NodeInfo? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
            string.Equals(Address, other.Address, StringComparison.Ordinal) &&
            GrpcPort == other.GrpcPort &&
            HealthPort == other.HealthPort &&
            Version == other.Version &&
            Shards.SequenceEqual(other.Shards);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(NodeId, StringComparer.Ordinal);
        hashCode.Add(Address, StringComparer.Ordinal);
        hashCode.Add(GrpcPort);
        hashCode.Add(HealthPort);
        hashCode.Add(Version, StringComparer.Ordinal);

        foreach (var shard in Shards)
        {
            hashCode.Add(shard);
        }

        return hashCode.ToHashCode();
    }
}
