using DistributedQuery.Worker;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.Worker;

public sealed class WorkerHealthServiceTests
{
    [Fact]
    public async Task IsReadyAsync_ReturnsTrue_WhenShardDatabaseResponds()
    {
        await using var database = await SqliteShardTestDatabase.CreateAsync();
        var lifecycleState = new WorkerLifecycleState();
        var registration = CreateRegistration(lifecycleState, database.ConnectionString);
        await registration.StartAsync(CancellationToken.None);

        var healthService = CreateHealthService(lifecycleState, database.ConnectionString);

        var ready = await healthService.IsReadyAsync(CancellationToken.None);

        ready.Should().BeTrue();
    }

    [Fact]
    public async Task IsReadyAsync_ReturnsFalse_WhenWorkerIsDraining()
    {
        await using var database = await SqliteShardTestDatabase.CreateAsync();
        var lifecycleState = new WorkerLifecycleState();
        var registration = CreateRegistration(
            lifecycleState,
            database.ConnectionString,
            drainTimeoutSeconds: 0);
        await registration.StartAsync(CancellationToken.None);
        await registration.StopAsync(CancellationToken.None);

        var healthService = CreateHealthService(lifecycleState, database.ConnectionString);

        var ready = await healthService.IsReadyAsync(CancellationToken.None);

        ready.Should().BeFalse();
    }

    private static WorkerRegistration CreateRegistration(
        WorkerLifecycleState lifecycleState,
        string connectionString,
        int drainTimeoutSeconds = 15)
    {
        return new WorkerRegistration(
            Options.Create(new WorkerOptions
            {
                NodeId = "test-worker",
                ShardIndices = [0],
                Shards = new Dictionary<string, string> { ["0"] = connectionString },
                Execution = new WorkerExecutionOptions { DrainTimeoutSeconds = drainTimeoutSeconds }
            }),
            lifecycleState,
            NullLogger<WorkerRegistration>.Instance);
    }

    private static WorkerHealthService CreateHealthService(
        WorkerLifecycleState lifecycleState,
        string connectionString)
    {
        var options = Options.Create(new WorkerOptions
        {
            NodeId = "test-worker",
            ShardIndices = [0],
            Shards = new Dictionary<string, string> { ["0"] = connectionString }
        });

        return new WorkerHealthService(
            options,
            lifecycleState,
            new ShardConnectionResolver(options),
            NullLogger<WorkerHealthService>.Instance);
    }
}
