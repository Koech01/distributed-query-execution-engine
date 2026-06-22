using System.Diagnostics;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Grpc;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Coordinator;

public sealed class CoordinatorAdminService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Coordinator.Admin");

    private readonly ActiveQueryRegistry _activeQueryRegistry;
    private readonly INodeRegistry _nodeRegistry;
    private readonly IQueryCacheAdmin _queryCacheAdmin;
    private readonly WorkerHealthProbe _workerHealthProbe;
    private readonly ILogger<CoordinatorAdminService> _logger;

    public CoordinatorAdminService(
        ActiveQueryRegistry activeQueryRegistry,
        INodeRegistry nodeRegistry,
        IQueryCacheAdmin queryCacheAdmin,
        WorkerHealthProbe workerHealthProbe,
        ILogger<CoordinatorAdminService> logger)
    {
        _activeQueryRegistry = activeQueryRegistry ?? throw new ArgumentNullException(nameof(activeQueryRegistry));
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _queryCacheAdmin = queryCacheAdmin ?? throw new ArgumentNullException(nameof(queryCacheAdmin));
        _workerHealthProbe = workerHealthProbe ?? throw new ArgumentNullException(nameof(workerHealthProbe));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("coordinator.admin.dashboard", ActivityKind.Internal);

        var workers = await _nodeRegistry.GetHealthyNodesAsync(cancellationToken).ConfigureAwait(false);
        var cacheStats = await _queryCacheAdmin.GetStatsAsync(cancellationToken).ConfigureAwait(false);
        var healthyWorkers = workers.Count;

        var stats = new AdminDashboardStats(
            _activeQueryRegistry.ActiveCount,
            healthyWorkers,
            healthyWorkers,
            cacheStats.PlanCacheEntries,
            cacheStats.ResultCacheEntries,
            cacheStats.AsyncQueryStatusEntries,
            DateTimeOffset.UtcNow);

        _logger.LogInformation(
            "Resolved admin dashboard stats. ActiveQueries={ActiveQueries}, HealthyWorkers={HealthyWorkers}, PlanCacheEntries={PlanCacheEntries}",
            stats.ActiveQueries,
            stats.HealthyWorkers,
            stats.PlanCacheEntries);

        return stats;
    }

    public Task<ActiveQueryPage> GetActiveQueriesAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = ActivitySource.StartActivity("coordinator.admin.active_queries", ActivityKind.Internal);
        return Task.FromResult(_activeQueryRegistry.List(limit, offset));
    }

    public Task<CancelQueryResult> CancelQueryAsync(Guid queryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = ActivitySource.StartActivity("coordinator.admin.cancel_query", ActivityKind.Internal);
        activity?.SetTag("query.id", queryId.ToString("D"));

        var result = _activeQueryRegistry.Cancel(queryId);
        _logger.LogInformation(
            "Admin cancel query {QueryId}: found={Found}, cancellationRequested={CancellationRequested}",
            queryId,
            result.Found,
            result.CancellationRequested);

        return Task.FromResult(result);
    }

    public async Task<WorkerHealthDashboard> GetWorkerHealthAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("coordinator.admin.worker_health", ActivityKind.Internal);

        var workers = await _nodeRegistry.GetHealthyNodesAsync(cancellationToken).ConfigureAwait(false);
        var probes = new List<WorkerHealthEntry>(workers.Count);

        foreach (var worker in workers)
        {
            probes.Add(await _workerHealthProbe.ProbeAsync(worker, cancellationToken).ConfigureAwait(false));
        }

        var healthyCount = probes.Count(static entry =>
            entry.LiveStatus == WorkerProbeStatus.Healthy &&
            entry.ReadyStatus == WorkerProbeStatus.Healthy &&
            entry.GrpcStatus == WorkerProbeStatus.Healthy);

        _logger.LogInformation(
            "Resolved worker health dashboard for {TotalWorkers} workers, {HealthyWorkers} fully healthy",
            probes.Count,
            healthyCount);

        return new WorkerHealthDashboard(
            probes,
            healthyCount,
            probes.Count,
            DateTimeOffset.UtcNow);
    }
}
