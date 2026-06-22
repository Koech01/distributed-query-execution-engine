namespace DistributedQuery.Core.Exceptions;

public sealed class ShardExecutionException : Exception
{
    public int ShardIndex { get; }
    public Guid SubQueryId { get; }

    public ShardExecutionException(int shardIndex, Guid subQueryId, string message)
        : base(message)
    {
        ShardIndex = shardIndex;
        SubQueryId = subQueryId;
    }

    public ShardExecutionException(int shardIndex, Guid subQueryId, string message, Exception inner)
        : base(message, inner)
    {
        ShardIndex = shardIndex;
        SubQueryId = subQueryId;
    }
}
