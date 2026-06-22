using System.Diagnostics;
using System.Runtime.CompilerServices;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Coordinator;

public sealed class CoordinatorService : IHostedService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Coordinator");

    private readonly QueryPlanningService _queryPlanningService;
    private readonly INodeRegistry _nodeRegistry;
    private readonly WorkerRouter _workerRouter;
    private readonly FanOutService _fanOutService;
    private readonly IResultMerger _resultMerger;
    private readonly IQueryCache _queryCache;
    private readonly ActiveQueryRegistry _activeQueryRegistry;
    private readonly CoordinatorOptions _options;
    private readonly ILogger<CoordinatorService> _logger;

    public CoordinatorService(
        QueryPlanningService queryPlanningService,
        INodeRegistry nodeRegistry,
        WorkerRouter workerRouter,
        FanOutService fanOutService,
        IResultMerger resultMerger,
        IQueryCache queryCache,
        ActiveQueryRegistry activeQueryRegistry,
        IOptions<CoordinatorOptions> options,
        ILogger<CoordinatorService> logger)
    {
        _queryPlanningService = queryPlanningService ?? throw new ArgumentNullException(nameof(queryPlanningService));
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _workerRouter = workerRouter ?? throw new ArgumentNullException(nameof(workerRouter));
        _fanOutService = fanOutService ?? throw new ArgumentNullException(nameof(fanOutService));
        _resultMerger = resultMerger ?? throw new ArgumentNullException(nameof(resultMerger));
        _queryCache = queryCache ?? throw new ArgumentNullException(nameof(queryCache));
        _activeQueryRegistry = activeQueryRegistry ?? throw new ArgumentNullException(nameof(activeQueryRegistry));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CoordinatorService started.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CoordinatorService stopped.");
        return Task.CompletedTask;
    }

    public async Task<QueryResult> ExecuteQueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("coordinator.query.execute", ActivityKind.Server);
        activity?.SetTag("query.id", request.QueryId.ToString("D"));

        var timeout = ResolveTimeout(request.Timeout);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var startedAt = Stopwatch.GetTimestamp();
        var (plan, _) = await _queryPlanningService.PlanAsync(request, timeoutCts.Token).ConfigureAwait(false);

        using var activeScope = _activeQueryRegistry.BeginQuery(
            request.QueryId,
            request.Async ? ActiveQueryKind.Async : ActiveQueryKind.Sync,
            plan.PlanHash,
            plan.SubQueries.Count);
        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, activeScope.CancellationToken);

        var healthyNodes = await _nodeRegistry.GetHealthyNodesAsync(executionCts.Token).ConfigureAwait(false);
        var assignments = _workerRouter.Route(plan.SubQueries, healthyNodes);
        CoordinatorObservability.RecordFanOutSize(assignments.Count);
        var partialResults = await _fanOutService.ExecuteAsync(assignments, executionCts.Token).ConfigureAwait(false);

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        var elapsedMs = (long)elapsed.TotalMilliseconds;
        var queryResult = _resultMerger.Merge(request.QueryId, partialResults, plan.MergeInstructions, elapsedMs);
        CoordinatorObservability.RecordQueryCompletion(queryResult, elapsed);

        await _queryCache
            .SetResultAsync(request.QueryId, queryResult, TimeSpan.FromSeconds(_options.ResultCacheTtlSeconds), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Coordinator completed query {QueryId} with {Rows} row(s), degraded={Degraded}",
            request.QueryId,
            queryResult.RowCount,
            queryResult.Degraded);

        return queryResult;
    }

    public async Task<QueryPlanDetails> PlanQueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("coordinator.query.plan", ActivityKind.Server);
        activity?.SetTag("query.id", request.QueryId.ToString("D"));

        var (plan, fromCache) = await _queryPlanningService.PlanAsync(request, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("cache.hit", fromCache);
        activity?.SetTag("plan.sub_query_count", plan.SubQueries.Count);

        _logger.LogInformation(
            "Resolved query plan for {QueryId}. SubQueries={SubQueryCount}, FromCache={FromCache}",
            request.QueryId,
            plan.SubQueries.Count,
            fromCache);

        return QueryPlanMapper.ToDetails(plan, fromCache);
    }

    public async IAsyncEnumerable<QueryStreamEvent> StreamExecuteQueryAsync(
        QueryRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("coordinator.query.stream", ActivityKind.Server);
        activity?.SetTag("query.id", request.QueryId.ToString("D"));

        var timeout = ResolveTimeout(request.Timeout);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var startedAt = Stopwatch.GetTimestamp();
        var (plan, _) = await _queryPlanningService.PlanAsync(request, timeoutCts.Token).ConfigureAwait(false);

        using var activeScope = _activeQueryRegistry.BeginQuery(
            request.QueryId,
            ActiveQueryKind.Stream,
            plan.PlanHash,
            plan.SubQueries.Count);
        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, activeScope.CancellationToken);

        var streamMode = QueryPlanMapper.ResolveStreamMode(plan.MergeInstructions);
        var totalShards = plan.SubQueries.Count > 0 ? plan.SubQueries[0].TotalShards : 0;

        yield return new QueryStreamEvent(
            QueryStreamEventKind.Metadata,
            QueryId: request.QueryId,
            TotalShards: totalShards,
            StreamMode: streamMode);

        var healthyNodes = await _nodeRegistry.GetHealthyNodesAsync(executionCts.Token).ConfigureAwait(false);
        var assignments = _workerRouter.Route(plan.SubQueries, healthyNodes);
        CoordinatorObservability.RecordFanOutSize(assignments.Count);

        var trackedPartials = new List<PartialResult>();
        var streamedRows = new List<IReadOnlyList<string>>();
        IReadOnlyList<string> columns = [];

        async IAsyncEnumerable<PartialResult> TrackPartials(
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var partial in _fanOutService.StreamAsync(assignments, ct).ConfigureAwait(false))
            {
                trackedPartials.Add(partial);
                yield return partial;
            }
        }

        await foreach (var streamEvent in _resultMerger
                           .StreamMergeAsync(request.QueryId, TrackPartials(executionCts.Token), plan.MergeInstructions, executionCts.Token)
                           .ConfigureAwait(false))
        {
            if (streamEvent.Kind == QueryStreamEventKind.Columns && streamEvent.Columns is not null)
            {
                columns = streamEvent.Columns;
            }
            else if (streamEvent.Kind == QueryStreamEventKind.Row && streamEvent.Row is not null)
            {
                streamedRows.Add(streamEvent.Row);
            }

            yield return streamEvent;
        }

        var failedShards = trackedPartials
            .Where(static result => !result.IsSuccess)
            .Select(static result => result.ShardIndex)
            .Distinct()
            .OrderBy(static index => index)
            .ToArray();
        var shardCount = trackedPartials
            .Select(static result => result.ShardIndex)
            .Distinct()
            .Count();
        if (shardCount == 0)
        {
            shardCount = assignments.Count;
        }

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        var elapsedMs = (long)elapsed.TotalMilliseconds;
        var complete = new QueryStreamCompletePayload(
            streamedRows.Count,
            shardCount,
            shardCount - failedShards.Length,
            failedShards,
            failedShards.Length > 0,
            failedShards.Length > 0
                ? $"{failedShards.Length} of {shardCount} shards failed: [{string.Join(", ", failedShards)}]"
                : null,
            elapsedMs);

        var queryResult = QueryResult.Create(
            request.QueryId,
            columns,
            streamedRows,
            shardCount,
            failedShards,
            elapsedMs);
        CoordinatorObservability.RecordQueryCompletion(queryResult, elapsed);

        await _queryCache
            .SetResultAsync(request.QueryId, queryResult, TimeSpan.FromSeconds(_options.ResultCacheTtlSeconds), cancellationToken)
            .ConfigureAwait(false);

        activity?.SetTag("result.row_count", streamedRows.Count);
        activity?.SetTag("result.degraded", failedShards.Length > 0);

        _logger.LogInformation(
            "Coordinator streamed query {QueryId} with {Rows} row(s), degraded={Degraded}",
            request.QueryId,
            streamedRows.Count,
            failedShards.Length > 0);

        yield return new QueryStreamEvent(QueryStreamEventKind.Complete, Complete: complete);
    }

    private TimeSpan ResolveTimeout(TimeSpan? requestedTimeout)
    {
        var configured = TimeSpan.FromMilliseconds(_options.DefaultQueryTimeoutMs);
        if (requestedTimeout is null)
        {
            return configured;
        }

        var max = TimeSpan.FromMilliseconds(_options.MaxQueryTimeoutMs);
        return requestedTimeout.Value <= max ? requestedTimeout.Value : max;
    }
}
