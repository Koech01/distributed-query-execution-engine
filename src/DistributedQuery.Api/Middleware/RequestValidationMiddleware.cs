using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DistributedQuery.Api.Contracts;
using DistributedQuery.Api.Options;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Api.Middleware;

public sealed partial class RequestValidationMiddleware
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.RequestValidationMiddleware");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [GeneratedRegex(@"^@[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterNamePattern();

    private readonly RequestDelegate _next;
    private readonly ApiOptions _options;

    public RequestValidationMiddleware(RequestDelegate next, IOptions<ApiOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) ||
            !context.Request.Path.StartsWithSegments("/queries", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        using var activity = ActivitySource.StartActivity("api.request.validate", ActivityKind.Internal);

        context.Request.EnableBuffering();

        SubmitQueryRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<SubmitQueryRequest>(
                context.Request.Body,
                JsonOptions,
                context.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("invalid_json", "Request body is not valid JSON."),
                context.RequestAborted).ConfigureAwait(false);
            return;
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        if (payload is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("validation_error", "Request body is required."),
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var validationError = ValidatePayload(payload);
        if (validationError is not null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, validationError);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("validation_error", validationError),
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private string? ValidatePayload(SubmitQueryRequest payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Sql))
        {
            return "SQL is required.";
        }

        if (payload.Sql.Contains('\0', StringComparison.Ordinal))
        {
            return "SQL must not contain null bytes.";
        }

        if (payload.Sql.Length > _options.MaxSqlLengthChars)
        {
            return $"SQL exceeds maximum length of {_options.MaxSqlLengthChars} characters.";
        }

        if (payload.Parameters.Count > _options.MaxParameters)
        {
            return $"Queries support at most {_options.MaxParameters} parameters.";
        }

        foreach (var parameter in payload.Parameters)
        {
            if (!ParameterNamePattern().IsMatch(parameter.Name))
            {
                return $"Parameter name '{parameter.Name}' is invalid.";
            }
        }

        if (payload.TimeoutSeconds is not null &&
            (payload.TimeoutSeconds < _options.MinTimeoutSeconds || payload.TimeoutSeconds > _options.MaxTimeoutSeconds))
        {
            return $"Timeout must be between {_options.MinTimeoutSeconds} and {_options.MaxTimeoutSeconds} seconds.";
        }

        if (payload.MaxNodes is { } maxNodes && (maxNodes < 1 || maxNodes > _options.MaxNodes))
        {
            return $"MaxNodes must be between 1 and {_options.MaxNodes} when provided.";
        }

        return null;
    }
}
