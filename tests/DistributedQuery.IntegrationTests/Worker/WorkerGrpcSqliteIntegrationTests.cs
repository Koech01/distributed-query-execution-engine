using DistributedQuery.Core.Interfaces;
using DistributedQuery.Infrastructure.Grpc;
using DistributedQuery.Worker;
using DistributedQuery.Worker.Services;
using Microsoft.Data.Sqlite;
using FluentAssertions;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SQLitePCL;

namespace DistributedQuery.IntegrationTests.Worker;

public sealed class WorkerGrpcSqliteIntegrationTests
{
    [Fact]
    public async Task ExecuteSubQuery_StreamsSqliteRowsThroughWorkerGrpcService()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        await using var database = await SqliteTestDatabase.CreateAsync("worker-grpc-integration");
        await database.ExecuteNonQueryAsync(
            "CREATE TABLE orders (id INTEGER PRIMARY KEY, amount INTEGER NOT NULL);");
        await database.ExecuteNonQueryAsync(
            "INSERT INTO orders (id, amount) VALUES (1, 100), (2, 200);");

        var port = Random.Shared.Next(10000, 60000);
        var baseAddress = $"http://127.0.0.1:{port}";

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(baseAddress);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(port, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
        });

        builder.Services.AddSingleton(Options.Create(new WorkerOptions
        {
            NodeId = "integration-worker",
            ShardIndices = [0],
            Shards = new Dictionary<string, string> { ["0"] = database.ConnectionString },
            Execution = new WorkerExecutionOptions { StreamChunkSize = 1, CommandTimeoutSeconds = 5 }
        }));
        builder.Services.AddSingleton<ShardConnectionResolver>();
        builder.Services.AddSingleton<ShardExecutor>();
        builder.Services.AddSingleton<ISubQueryExecutor>(sp => sp.GetRequiredService<ShardExecutor>());
        builder.Services.AddGrpc(options => options.Interceptors.Add<TracingServerInterceptor>());

        var app = builder.Build();
        app.MapGrpcService<WorkerGrpcService>();
        await app.StartAsync();

        try
        {
            using var channel = GrpcChannel.ForAddress(baseAddress);
            var client = new QueryExecution.QueryExecutionClient(channel);
            var subQueryId = Guid.NewGuid();
            var request = new SubQueryRequest
            {
                SubQueryId = subQueryId.ToString("D"),
                ParentQueryId = Guid.NewGuid().ToString("D"),
                Sql = "SELECT id, amount FROM orders ORDER BY id",
                ShardIndex = 0,
                TotalShards = 1,
                TimeoutMs = 5000
            };

            using var call = client.ExecuteSubQuery(request);
            var messages = new List<PartialResultResponse>();
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                messages.Add(call.ResponseStream.Current);
            }

            messages.Should().NotBeEmpty();
            messages[0].PayloadCase.Should().Be(PartialResultResponse.PayloadOneofCase.Meta);
            messages.Count(static message => message.PayloadCase == PartialResultResponse.PayloadOneofCase.Chunk)
                .Should()
                .Be(2);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private static int _providerInitialized;
        private readonly SqliteConnection _keeperConnection;

        private SqliteTestDatabase(SqliteConnection keeperConnection, string connectionString)
        {
            _keeperConnection = keeperConnection;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<SqliteTestDatabase> CreateAsync(string databaseName)
        {
            InitializeProvider();

            var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
            var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();
            return new SqliteTestDatabase(keeperConnection, connectionString);
        }

        public async Task ExecuteNonQueryAsync(string sql)
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync() => await _keeperConnection.DisposeAsync();

        private static void InitializeProvider()
        {
            if (Interlocked.Exchange(ref _providerInitialized, 1) == 1)
            {
                return;
            }

            raw.SetProvider(new SQLite3Provider_sqlite3());
        }
    }
}
