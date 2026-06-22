using DistributedQuery.Worker;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.Worker;

public sealed class WorkerRegistrationTests
{
    [Fact]
    public async Task StopAsync_MarksWorkerAsDraining_AndNotReady()
    {
        var lifecycleState = new WorkerLifecycleState();
        var registration = CreateRegistration(lifecycleState, drainTimeoutSeconds: 0);

        await registration.StartAsync(CancellationToken.None);
        lifecycleState.IsReady.Should().BeTrue();

        await registration.StopAsync(CancellationToken.None);

        lifecycleState.IsReady.Should().BeFalse();
        lifecycleState.IsDraining.Should().BeTrue();
    }

    private static WorkerRegistration CreateRegistration(
        WorkerLifecycleState lifecycleState,
        int drainTimeoutSeconds)
    {
        return new WorkerRegistration(
            Options.Create(new WorkerOptions
            {
                NodeId = "test-worker",
                Execution = new WorkerExecutionOptions { DrainTimeoutSeconds = drainTimeoutSeconds }
            }),
            lifecycleState,
            NullLogger<WorkerRegistration>.Instance);
    }
}
