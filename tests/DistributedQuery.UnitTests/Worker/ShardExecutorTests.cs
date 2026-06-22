using DistributedQuery.Core.Models;
using DistributedQuery.Worker;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.Worker;

public sealed class ShardExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_StreamsRowsInChunks_WithSchemaOnFirstChunkOnly()
    {
        await using var database = await SqliteShardTestDatabase.CreateAsync(Guid.NewGuid().ToString("N"));
        await database.ExecuteNonQueryAsync(
            "CREATE TABLE orders (id INTEGER PRIMARY KEY, amount INTEGER NOT NULL);");
        await database.ExecuteNonQueryAsync(
            "INSERT INTO orders (id, amount) VALUES (1, 100), (2, 200), (3, 300);");

        var executor = CreateExecutor(database.ConnectionString, streamChunkSize: 2);
        var subQuery = new SubQuery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SELECT id, amount FROM orders ORDER BY id",
            string.Empty,
            0,
            4,
            Array.Empty<QueryParameter>());

        var results = new List<PartialResult>();
        await foreach (var result in executor.ExecuteAsync(subQuery, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().HaveCount(2);
        results[0].Columns.Should().HaveCount(2);
        results[0].Rows.Should().HaveCount(2);
        results[1].Columns.Should().BeEmpty();
        results[1].Rows.Should().HaveCount(1);
        results.SelectMany(static result => result.Rows).Should().HaveCount(3);
    }

    [Fact]
    public async Task ExecuteAsync_BindsJsonParameters()
    {
        await using var database = await SqliteShardTestDatabase.CreateAsync(Guid.NewGuid().ToString("N"));
        await database.ExecuteNonQueryAsync(
            "CREATE TABLE orders (id INTEGER PRIMARY KEY, amount INTEGER NOT NULL);");
        await database.ExecuteNonQueryAsync(
            "INSERT INTO orders (id, amount) VALUES (1, 100), (2, 200);");

        var executor = CreateExecutor(database.ConnectionString, streamChunkSize: 10);
        var subQuery = new SubQuery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SELECT amount FROM orders WHERE amount > @minAmount",
            string.Empty,
            0,
            1,
            new[] { new QueryParameter("minAmount", "int", "150") });

        var results = new List<PartialResult>();
        await foreach (var result in executor.ExecuteAsync(subQuery, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().ContainSingle();
        results[0].Rows.Should().ContainSingle().Which.Should().ContainSingle().Which.Should().Be("200");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsArgumentException_WhenShardIsNotOwned()
    {
        var executor = CreateExecutor("Data Source=:memory:", streamChunkSize: 10, shardIndices: [0]);
        var subQuery = new SubQuery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SELECT 1",
            string.Empty,
            9,
            10,
            Array.Empty<QueryParameter>());

        Func<Task> act = async () =>
        {
            await foreach (var _ in executor.ExecuteAsync(subQuery, CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static ShardExecutor CreateExecutor(
        string connectionString,
        int streamChunkSize,
        IReadOnlyList<int>? shardIndices = null)
    {
        var options = Options.Create(new WorkerOptions
        {
            NodeId = "test-worker",
            ShardIndices = shardIndices ?? [0],
            Shards = new Dictionary<string, string> { ["0"] = connectionString },
            Execution = new WorkerExecutionOptions
            {
                StreamChunkSize = streamChunkSize,
                CommandTimeoutSeconds = 5,
                MaxConcurrentQueries = 2
            }
        });

        return new ShardExecutor(
            options,
            new ShardConnectionResolver(options),
            NullLogger<ShardExecutor>.Instance);
    }
}
