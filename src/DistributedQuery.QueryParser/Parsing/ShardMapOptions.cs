namespace DistributedQuery.QueryParser.Parsing;

public sealed class ShardMapOptions
{
    public Dictionary<string, TableShardConfig> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TableShardConfig
{
    public string ShardKey { get; init; } = string.Empty;
    public int ShardCount { get; init; }
    public string Strategy { get; init; } = "ConsistentHash";
    public List<RangePartitionEntry>? Ranges { get; init; }
}

public sealed class RangePartitionEntry
{
    public int Shard { get; init; }
    public string? Min { get; init; }
    public string? Max { get; init; }
}
