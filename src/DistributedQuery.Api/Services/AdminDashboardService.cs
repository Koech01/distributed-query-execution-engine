using System.Diagnostics;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DistributedQuery.Api.Services;

public sealed class AdminDashboardService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.AdminDashboardService");
    private static readonly TimeSpan StatsCacheTtl = TimeSpan.FromSeconds(30);
    private const string StatsCacheKey = "admin:dashboard-stats";

    private readonly IQueryCoordinatorClient _coordinatorClient;
    private readonly IQueryCacheAdmin _queryCacheAdmin;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminDashboardService> _logger;

    public AdminDashboardService(
        IQueryCoordinatorClient coordinatorClient,
        IQueryCacheAdmin queryCacheAdmin,
        IMemoryCache memoryCache,
        ILogger<AdminDashboardService> logger)
    {
        _coordinatorClient = coordinatorClient ?? throw new ArgumentNullException(nameof(coordinatorClient));
        _queryCacheAdmin = queryCacheAdmin ?? throw new ArgumentNullException(nameof(queryCacheAdmin));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminDashboardStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(StatsCacheKey, out AdminDashboardStats? cached) && cached is not null)
        {
            return cached;
        }

        using var activity = ActivitySource.StartActivity("api.admin.dashboard", ActivityKind.Internal);

        var coordinatorStats = await _coordinatorClient.GetAdminDashboardAsync(cancellationToken).ConfigureAwait(false);
        var cacheStats = await _queryCacheAdmin.GetStatsAsync(cancellationToken).ConfigureAwait(false);

        var stats = coordinatorStats with
        {
            PlanCacheEntries = cacheStats.PlanCacheEntries,
            ResultCacheEntries = cacheStats.ResultCacheEntries,
            AsyncQueryStatusEntries = cacheStats.AsyncQueryStatusEntries,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        _memoryCache.Set(StatsCacheKey, stats, StatsCacheTtl);
        _logger.LogInformation(
            "Resolved admin dashboard stats. ActiveQueries={ActiveQueries}, HealthyWorkers={HealthyWorkers}",
            stats.ActiveQueries,
            stats.HealthyWorkers);

        return stats;
    }
}
