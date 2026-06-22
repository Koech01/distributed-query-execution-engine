using System.Diagnostics;
using System.Net;
using System.Text.Json;
using DistributedQuery.Api.Contracts;
using DistributedQuery.Core.Exceptions;

namespace DistributedQuery.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.ExceptionHandlingMiddleware");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex).ConfigureAwait(false);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            throw exception;
        }

        using var activity = ActivitySource.StartActivity("api.exception.handle", ActivityKind.Internal);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        var (statusCode, response) = MapException(exception);

        if (exception is AuthenticationException { Kind: AuthenticationFailureKind.AccountLocked } locked &&
            locked.LockedUntilUtc.HasValue)
        {
            var retryAfterSeconds = Math.Max(
                1,
                (int)Math.Ceiling((locked.LockedUntilUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
            context.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        }

        _logger.LogWarning(
            exception,
            "Request {Method} {Path} failed with {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            (int)statusCode);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response, JsonOptions, context.RequestAborted).ConfigureAwait(false);
    }

    private static (HttpStatusCode StatusCode, ErrorResponse Response) MapException(Exception exception) =>
        exception switch
        {
            QueryParseException parse => (
                HttpStatusCode.BadRequest,
                new ErrorResponse("query_parse_error", parse.Message, parse.ParseErrors)),
            InsufficientNodesException nodes => (
                HttpStatusCode.ServiceUnavailable,
                new ErrorResponse(
                    "insufficient_nodes",
                    nodes.Message,
                    [$"requiredShards={nodes.RequiredShards}", $"availableNodes={nodes.AvailableNodes}"])),
            QueryTimeoutException timeout => (
                HttpStatusCode.RequestTimeout,
                new ErrorResponse("query_timeout", timeout.Message)),
            ShardExecutionException shard => (
                HttpStatusCode.BadGateway,
                new ErrorResponse("shard_execution_error", shard.Message)),
            ShardConfigurationException config => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse("shard_configuration_error", config.Message)),
            AuthenticationException auth => auth.Kind switch
            {
                AuthenticationFailureKind.EmailAlreadyExists => (
                    HttpStatusCode.Conflict,
                    new ErrorResponse("email_already_exists", auth.Message)),
                AuthenticationFailureKind.AccountLocked => (
                    HttpStatusCode.Locked,
                    new ErrorResponse("account_locked", auth.Message)),
                AuthenticationFailureKind.AccountDeleted => (
                    HttpStatusCode.Unauthorized,
                    new ErrorResponse("account_deleted", auth.Message)),
                _ => (
                    HttpStatusCode.Unauthorized,
                    new ErrorResponse("authentication_failed", auth.Message))
            },
            OperationCanceledException => (
                (HttpStatusCode)499,
                new ErrorResponse("request_cancelled", "The request was cancelled.")),
            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse("internal_error", "An unexpected error occurred."))
        };
}
