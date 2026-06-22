using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Worker;

public sealed class WorkerHealthService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Worker.WorkerHealthService");

    private readonly WorkerOptions _options;
    private readonly WorkerLifecycleState _lifecycleState;
    private readonly ShardConnectionResolver _connectionResolver;
    private readonly ILogger<WorkerHealthService> _logger;

    public WorkerHealthService(
        IOptions<WorkerOptions> options,
        WorkerLifecycleState lifecycleState,
        ShardConnectionResolver connectionResolver,
        ILogger<WorkerHealthService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _lifecycleState = lifecycleState ?? throw new ArgumentNullException(nameof(lifecycleState));
        _connectionResolver = connectionResolver ?? throw new ArgumentNullException(nameof(connectionResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsLive() => true;

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        if (!_lifecycleState.IsReady || _lifecycleState.IsDraining)
        {
            return false;
        }

        using var activity = ActivitySource.StartActivity("WorkerHealthService.Readiness", ActivityKind.Internal);
        activity?.SetTag("worker.node_id", _options.NodeId);

        foreach (var shardIndex in _options.ShardIndices.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await PingShardAsync(shardIndex, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(
                    ex,
                    "Readiness check failed for shard {ShardIndex}",
                    shardIndex);

                return false;
            }
        }

        return true;
    }

    private async Task PingShardAsync(int shardIndex, CancellationToken cancellationToken)
    {
        await using var connection = _connectionResolver.CreateConnection(shardIndex);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = 5;
        _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }
}
