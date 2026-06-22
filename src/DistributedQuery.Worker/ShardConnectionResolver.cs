using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SQLitePCL;

namespace DistributedQuery.Worker;

public sealed class ShardConnectionResolver
{
    private static readonly object SqliteInitLock = new();
    private static volatile bool _sqliteProviderInitialized;
    private readonly WorkerOptions _options;

    public ShardConnectionResolver(IOptions<WorkerOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string GetConnectionString(int shardIndex)
    {
        var key = shardIndex.ToString();
        if (_options.Shards.TryGetValue(key, out var connectionString) &&
            !string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException(
            $"No connection string configured for shard index {shardIndex}.");
    }

    public DbConnection CreateConnection(int shardIndex)
    {
        var connectionString = GetConnectionString(shardIndex);
        if (IsSqlServerConnectionString(connectionString))
        {
            return new SqlConnection(connectionString);
        }

        InitializeSqliteProvider();
        return new SqliteConnection(connectionString);
    }

    private static bool IsSqlServerConnectionString(string connectionString)
    {
        return connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("User ID=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Trusted_Connection=", StringComparison.OrdinalIgnoreCase) ||
               (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) &&
                !connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase));
    }

    private static void InitializeSqliteProvider()
    {
        if (_sqliteProviderInitialized)
        {
            return;
        }

        lock (SqliteInitLock)
        {
            if (_sqliteProviderInitialized)
            {
                return;
            }

            raw.SetProvider(new SQLite3Provider_sqlite3());
            _sqliteProviderInitialized = true;
        }
    }
}
