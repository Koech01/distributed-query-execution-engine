using System.Diagnostics;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Coordinator;

public sealed class QueryPlanningService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Coordinator");

    private readonly IQueryCache _queryCache;
    private readonly IQueryPlanner _queryPlanner;
    private readonly CoordinatorOptions _options;
    private readonly ILogger<QueryPlanningService> _logger;

    public QueryPlanningService(
        IQueryCache queryCache,
        IQueryPlanner queryPlanner,
        IOptions<CoordinatorOptions> options,
        ILogger<QueryPlanningService> logger)
    {
        _queryCache = queryCache ?? throw new ArgumentNullException(nameof(queryCache));
        _queryPlanner = queryPlanner ?? throw new ArgumentNullException(nameof(queryPlanner));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(QueryPlan Plan, bool FromCache)> PlanAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("coordinator.plan.resolve", ActivityKind.Internal);
        activity?.SetTag("query.id", request.QueryId.ToString("D"));
        activity?.SetTag("query.sql_hash", CacheKeyBuilder.ForPlan(request.Sql, request.Parameters));

        var cacheKey = CacheKeyBuilder.ForPlan(request.Sql, request.Parameters);
        var cachedPlan = await _queryCache.TryGetPlanAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cachedPlan is not null)
        {
            activity?.SetTag("cache.hit", true);
            CoordinatorObservability.RecordPlanCache(fromCache: true);
            _logger.LogInformation("Plan cache hit for query {QueryId}", request.QueryId);
            return (cachedPlan, true);
        }

        activity?.SetTag("cache.hit", false);
        CoordinatorObservability.RecordPlanCache(fromCache: false);
        _logger.LogInformation("Plan cache miss for query {QueryId}. Planning query.", request.QueryId);

        var plan = await _queryPlanner.PlanAsync(request, cancellationToken).ConfigureAwait(false);

        await _queryCache
            .SetPlanAsync(cacheKey, plan, TimeSpan.FromSeconds(_options.PlanCacheTtlSeconds), cancellationToken)
            .ConfigureAwait(false);

        return (plan, false);
    }
}
