using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Worker;

public sealed class WorkerRegistration : IHostedService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Worker.WorkerRegistration");

    private readonly WorkerOptions _options;
    private readonly WorkerLifecycleState _lifecycleState;
    private readonly ILogger<WorkerRegistration> _logger;

    public WorkerRegistration(
        IOptions<WorkerOptions> options,
        WorkerLifecycleState lifecycleState,
        ILogger<WorkerRegistration> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _lifecycleState = lifecycleState ?? throw new ArgumentNullException(nameof(lifecycleState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("WorkerRegistration.Start", ActivityKind.Internal);
        activity?.SetTag("worker.node_id", _options.NodeId);

        _lifecycleState.MarkReady();

        _logger.LogInformation(
            "Worker {NodeId} is ready to accept sub-queries on gRPC port {GrpcPort}",
            _options.NodeId,
            _options.GrpcPort);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("WorkerRegistration.Stop", ActivityKind.Internal);
        activity?.SetTag("worker.node_id", _options.NodeId);

        _lifecycleState.MarkDraining();

        var drainTimeout = TimeSpan.FromSeconds(Math.Max(1, _options.Execution.DrainTimeoutSeconds));
        _logger.LogInformation(
            "Worker {NodeId} draining in-flight sub-queries for {DrainSeconds}s before shutdown",
            _options.NodeId,
            drainTimeout.TotalSeconds);

        try
        {
            await Task.Delay(drainTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Worker {NodeId} drain period was canceled before completion",
                _options.NodeId);
        }

        _logger.LogInformation("Worker {NodeId} drain period completed", _options.NodeId);
    }
}
