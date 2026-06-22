using System.Text.Json;
using System.Text.Json.Serialization;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace DistributedQuery.Coordinator;

public static class CoordinatorQueryEndpoints
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IEndpointRouteBuilder MapCoordinatorQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/queries/execute", ExecuteQueryAsync);
        endpoints.MapPost("/internal/v1/queries/submit", SubmitQueryAsync);
        endpoints.MapPost("/internal/v1/queries/plan", PlanQueryAsync);
        endpoints.MapPost("/internal/v1/queries/stream", StreamQueryAsync);
        return endpoints;
    }

    private static async Task<IResult> ExecuteQueryAsync(
        [FromBody] CoordinatorQueryPayload payload,
        CoordinatorService coordinatorService,
        CancellationToken cancellationToken)
    {
        if (payload is null)
        {
            return Results.BadRequest(CreateError(new QueryParseException("Request body is required", string.Empty, ["Missing body"])));
        }

        try
        {
            var request = payload.ToQueryRequest(async: false);
            var result = await coordinatorService.ExecuteQueryAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Json(result, JsonOptions);
        }
        catch (QueryParseException ex)
        {
            return Results.BadRequest(CreateError(ex));
        }
        catch (ShardConfigurationException ex)
        {
            return Results.BadRequest(CreateError(ToQueryParseException(ex)));
        }
        catch (InsufficientNodesException ex)
        {
            return Results.Json(CreateError(ex), statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (QueryTimeoutException ex)
        {
            return Results.Json(CreateError(ex), statusCode: StatusCodes.Status408RequestTimeout);
        }
    }

    private static async Task<IResult> SubmitQueryAsync(
        [FromBody] CoordinatorQueryPayload payload,
        QueryBackgroundDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (payload is null)
        {
            return Results.BadRequest(CreateError(
                new QueryParseException("Request body is required", string.Empty, ["Missing body"])));
        }

        var request = payload.ToQueryRequest(async: true);
        await dispatcher.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);
        return Results.Accepted($"/queries/{request.QueryId}/status");
    }

    private static async Task<IResult> PlanQueryAsync(
        [FromBody] CoordinatorQueryPayload payload,
        CoordinatorService coordinatorService,
        CancellationToken cancellationToken)
    {
        if (payload is null)
        {
            return Results.BadRequest(CreateError(new QueryParseException("Request body is required", string.Empty, ["Missing body"])));
        }

        try
        {
            var request = payload.ToQueryRequest(async: false);
            var plan = await coordinatorService.PlanQueryAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Json(plan, JsonOptions);
        }
        catch (QueryParseException ex)
        {
            return Results.BadRequest(CreateError(ex));
        }
        catch (ShardConfigurationException ex)
        {
            return Results.BadRequest(CreateError(ToQueryParseException(ex)));
        }
    }

    private static async Task StreamQueryAsync(
        [FromBody] CoordinatorQueryPayload payload,
        CoordinatorService coordinatorService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (payload is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                CreateError(new QueryParseException("Request body is required", string.Empty, ["Missing body"])),
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var request = payload.ToQueryRequest(async: false);
            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";

            await foreach (var streamEvent in coordinatorService
                               .StreamExecuteQueryAsync(request, cancellationToken)
                               .ConfigureAwait(false))
            {
                await CoordinatorStreamResponseWriter
                    .WriteEventAsync(httpContext.Response, streamEvent, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (QueryParseException ex)
        {
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(CreateError(ex), JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (ShardConfigurationException ex)
        {
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                        CreateError(ToQueryParseException(ex)),
                        JsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (InsufficientNodesException ex)
        {
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await httpContext.Response.WriteAsJsonAsync(CreateError(ex), JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (QueryTimeoutException ex)
        {
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                await httpContext.Response.WriteAsJsonAsync(CreateError(ex), JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static QueryParseException ToQueryParseException(ShardConfigurationException exception) =>
        new(
            exception.Message,
            string.Empty,
            [$"Table '{exception.TableName}' is not available in the distributed query engine."]);

    private static object CreateError(Exception exception) => exception switch
    {
        QueryParseException parse => new
        {
            type = nameof(QueryParseException),
            message = parse.Message,
            sqlHash = parse.SqlHash,
            parseErrors = parse.ParseErrors
        },
        InsufficientNodesException nodes => new
        {
            type = nameof(InsufficientNodesException),
            message = nodes.Message,
            requiredShards = nodes.RequiredShards,
            availableNodes = nodes.AvailableNodes
        },
        QueryTimeoutException timeout => new
        {
            type = nameof(QueryTimeoutException),
            message = timeout.Message,
            queryId = timeout.QueryId,
            timeoutMs = (int)timeout.Timeout.TotalMilliseconds
        },
        _ => new { type = exception.GetType().Name, message = exception.Message }
    };

    private sealed record CoordinatorQueryPayload(
        Guid QueryId,
        string Sql,
        IReadOnlyList<QueryParameter> Parameters,
        int? MaxNodes,
        int? TimeoutMs,
        bool Async,
        string FailurePolicyName)
    {
        public QueryRequest ToQueryRequest(bool async)
        {
            var policy = Enum.TryParse<FailurePolicy>(FailurePolicyName, ignoreCase: true, out var parsed)
                ? parsed
                : FailurePolicy.BestEffort;

            TimeSpan? timeout = TimeoutMs is null or <= 0
                ? null
                : TimeSpan.FromMilliseconds(TimeoutMs.Value);

            return new QueryRequest(
                QueryId,
                Sql,
                Parameters ?? [],
                MaxNodes,
                timeout,
                async,
                policy);
        }
    }
}
