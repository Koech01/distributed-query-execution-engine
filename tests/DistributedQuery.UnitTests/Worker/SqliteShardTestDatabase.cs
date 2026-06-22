using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace DistributedQuery.UnitTests.Worker;

internal sealed class SqliteShardTestDatabase : IAsyncDisposable
{
    private static int _providerInitialized;
    private readonly SqliteConnection _keeperConnection;

    private SqliteShardTestDatabase(SqliteConnection keeperConnection, string connectionString)
    {
        _keeperConnection = keeperConnection;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<SqliteShardTestDatabase> CreateAsync(string databaseName = "shard-test")
    {
        InitializeProvider();

        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
        var keeperConnection = new SqliteConnection(connectionString);
        await keeperConnection.OpenAsync();

        return new SqliteShardTestDatabase(keeperConnection, connectionString);
    }

    public async Task ExecuteNonQueryAsync(string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _keeperConnection.DisposeAsync();
    }

    private static void InitializeProvider()
    {
        if (Interlocked.Exchange(ref _providerInitialized, 1) == 1)
        {
            return;
        }

        raw.SetProvider(new SQLite3Provider_sqlite3());
    }
}
