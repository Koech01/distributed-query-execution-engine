using System.Diagnostics;
using System.Net;
using DistributedQuery.Api.Contracts;
using DistributedQuery.Api.Options;
using DistributedQuery.Api.Services;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Api.Controllers;

[ApiController]
[Route("queries")]
[Authorize(Policy = "QueryRead")]
public sealed class QueryController : ControllerBase
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.QueryController");

    private readonly IQueryCoordinatorClient _coordinatorClient;
    private readonly IQueryCache _queryCache;
    private readonly ApiOptions _options;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        IQueryCoordinatorClient coordinatorClient,
        IQueryCache queryCache,
        IOptions<ApiOptions> options,
        ILogger<QueryController> logger)
    {
        _coordinatorClient = coordinatorClient ?? throw new ArgumentNullException(nameof(coordinatorClient));
        _queryCache = queryCache ?? throw new ArgumentNullException(nameof(queryCache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [ProducesResponseType(typeof(QueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SubmitQueryResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(QueryResult), StatusCodes.Status206PartialContent)]
    public async Task<IActionResult> SubmitAsync(
        [FromBody] SubmitQueryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("api.query.submit", ActivityKind.Server);
        var queryRequest = MapToQueryRequest(request);
        activity?.SetTag("query.id", queryRequest.QueryId.ToString("D"));

        if (request.Async)
        {
            await _coordinatorClient.SubmitAsync(queryRequest, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Accepted async query {QueryId}", queryRequest.QueryId);

            return Accepted(
                new SubmitQueryResponse(queryRequest.QueryId, $"/queries/{queryRequest.QueryId}/status"));
        }

        var cached = await _queryCache
            .TryGetResultAsync(queryRequest.QueryId, cancellationToken)
            .ConfigureAwait(false);

        if (cached is not null)
        {
            activity?.SetTag("cache.hit", true);
            return ToResultAction(cached);
        }

        var result = await _coordinatorClient.ExecuteAsync(queryRequest, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Completed query {QueryId} with {RowCount} rows, degraded={Degraded}",
            result.QueryId,
            result.RowCount,
            result.Degraded);

        return ToResultAction(result);
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(QueryStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatusAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.query.status", ActivityKind.Server);
        activity?.SetTag("query.id", id.ToString("D"));

        var cached = await _queryCache.TryGetResultAsync(id, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return Ok(new QueryStatusResponse(id, "completed", null));
        }

        return Ok(new QueryStatusResponse(id, "running", null));
    }

    [HttpGet("{id:guid}/result")]
    [ProducesResponseType(typeof(QueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.query.result", ActivityKind.Server);
        activity?.SetTag("query.id", id.ToString("D"));

        var cached = await _queryCache.TryGetResultAsync(id, cancellationToken).ConfigureAwait(false);
        if (cached is null)
        {
            return NotFound(new ErrorResponse("not_found", $"No result found for query {id:D}."));
        }

        return ToResultAction(cached);
    }

    [HttpPost("plan")]
    [ProducesResponseType(typeof(QueryPlanDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PlanAsync(
        [FromBody] SubmitQueryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("api.query.plan", ActivityKind.Server);
        var queryRequest = MapToQueryRequest(request);
        activity?.SetTag("query.id", queryRequest.QueryId.ToString("D"));

        var plan = await _coordinatorClient.PlanAsync(queryRequest, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Resolved query plan for {QueryId}. SubQueries={SubQueryCount}, FromCache={FromCache}",
            queryRequest.QueryId,
            plan.SubQueries.Count,
            plan.FromCache);

        return Ok(plan);
    }

    [HttpPost("stream")]
    [Produces("text/event-stream")]
    public async Task StreamAsync(
        [FromBody] SubmitQueryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Async)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(
                new ErrorResponse("invalid_request", "Streaming is not supported for async queries."),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        using var activity = ActivitySource.StartActivity("api.query.stream", ActivityKind.Server);
        var queryRequest = MapToQueryRequest(request);
        activity?.SetTag("query.id", queryRequest.QueryId.ToString("D"));

        var cached = await _queryCache
            .TryGetResultAsync(queryRequest.QueryId, cancellationToken)
            .ConfigureAwait(false);

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        if (cached is not null)
        {
            activity?.SetTag("cache.hit", true);
            await WriteCachedStreamAsync(cached, cancellationToken).ConfigureAwait(false);
            return;
        }

        activity?.SetTag("cache.hit", false);

        await foreach (var streamEvent in _coordinatorClient
                           .StreamExecuteAsync(queryRequest, cancellationToken)
                           .ConfigureAwait(false))
        {
            switch (streamEvent.Kind)
            {
                case QueryStreamEventKind.Metadata when streamEvent.QueryId is not null:
                    await QueryStreamResponseWriter.WriteMetadataAsync(
                        Response,
                        streamEvent.QueryId.Value,
                        streamEvent.TotalShards ?? 0,
                        streamEvent.StreamMode ?? QueryStreamMode.Buffered,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case QueryStreamEventKind.Columns when streamEvent.Columns is not null:
                    await QueryStreamResponseWriter.WriteColumnsAsync(
                        Response,
                        streamEvent.Columns,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case QueryStreamEventKind.Row when streamEvent.Row is not null:
                    await QueryStreamResponseWriter.WriteRowAsync(
                        Response,
                        streamEvent.Row,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case QueryStreamEventKind.Complete when streamEvent.Complete is not null:
                    await QueryStreamResponseWriter.WriteCompleteAsync(
                        Response,
                        streamEvent.Complete,
                        cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async Task WriteCachedStreamAsync(QueryResult cached, CancellationToken cancellationToken)
    {
        await QueryStreamResponseWriter.WriteMetadataAsync(
            Response,
            cached.QueryId,
            cached.TotalShards,
            QueryStreamMode.Buffered,
            cancellationToken).ConfigureAwait(false);

        if (cached.Columns.Count > 0)
        {
            await QueryStreamResponseWriter.WriteColumnsAsync(Response, cached.Columns, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var row in cached.Rows)
        {
            await QueryStreamResponseWriter.WriteRowAsync(Response, row, cancellationToken).ConfigureAwait(false);
        }

        await QueryStreamResponseWriter.WriteCompleteAsync(
            Response,
            new QueryStreamCompletePayload(
                cached.RowCount,
                cached.TotalShards,
                cached.SuccessfulShards,
                cached.FailedShards,
                cached.Degraded,
                cached.DegradationReason,
                cached.ExecutionMs),
            cancellationToken).ConfigureAwait(false);
    }

    private QueryRequest MapToQueryRequest(SubmitQueryRequest request)
    {
        var timeout = request.TimeoutSeconds is null
            ? TimeSpan.FromSeconds(30)
            : TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds.Value, _options.MinTimeoutSeconds, _options.MaxTimeoutSeconds));

        var queryId = request.QueryId ?? Guid.NewGuid();
        var parameters = request.Parameters
            .Select(static p => new QueryParameter(p.Name, p.Type, p.Value))
            .ToList();

        return new QueryRequest(
            queryId,
            request.Sql,
            parameters,
            request.MaxNodes,
            timeout,
            request.Async,
            request.FailurePolicy);
    }

    private IActionResult ToResultAction(QueryResult result)
    {
        if (result.Degraded)
        {
            return StatusCode((int)HttpStatusCode.PartialContent, result);
        }

        return Ok(result);
    }
}
