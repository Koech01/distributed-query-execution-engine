namespace DistributedQuery.Core.Exceptions;

public sealed class ShardConfigurationException : Exception
{
    public string TableName { get; }

    public ShardConfigurationException(string tableName)
        : base($"No shard map configuration found for table '{tableName}'")
    {
        TableName = tableName;
    }
}
