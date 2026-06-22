using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Infrastructure.Coordinator;

public sealed class CoordinatorHttpClient : IQueryCoordinatorClient
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.CoordinatorHttpClient");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly CoordinatorClientOptions _options;
    private readonly ILogger<CoordinatorHttpClient> _logger;

    public CoordinatorHttpClient(
        HttpClient httpClient,
        IOptions<CoordinatorClientOptions> options,
        ILogger<CoordinatorHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("coordinator.client.execute", ActivityKind.Client);
        activity?.SetTag("query.id", request.QueryId.ToString("D"));

        var payload = CoordinatorQueryPayload.FromRequest(request, async: false);
        var response = await SendAsync(
                HttpMethod.Post,
                "/internal/v1/queries/execute",
                payload,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            throw await DeserializeExceptionAsync<InsufficientNodesException>(response, cancellationToken)
                .ConfigureAwait(false)
                ?? new InsufficientNodesException(0, 0);
        }

        if (response.StatusCode == HttpStatusCode.RequestTimeout)
        {
            throw await DeserializeExceptionAsync<QueryTimeoutException>(response, cancellationToken)
                .ConfigureAwait(false)
                ?? new QueryTimeoutException(request.QueryId, request.Timeout ?? TimeSpan.FromSeconds(30));
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw await DeserializeExceptionAsync<QueryParseException>(response, cancellationToken)
                .ConfigureAwait(false)
                ?? new QueryParseException("Query rejected by coordinator", string.Empty, ["Invalid query"]);
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<QueryResult>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            throw new InvalidOperationException("Coordinator returned an empty query result payload.");
        }

        _logger.LogInformation(
            "Coordinator execute completed for query {QueryId} with {RowCount} row(s)",
            request.QueryId,
            result.RowCount);

        return result;
    }

    public async Task SubmitAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("coordinator.client.submit", ActivityKind.Client);
        activity?.SetTag("query.id", request.QueryId.ToString("D"));

        var payload = CoordinatorQueryPayload.FromRequest(request, async: true);
        var response = await SendAsync(
                HttpMethod.Post,
                "/internal/v1/queries/submit",
                payload,
                cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "Coordinator submit failed for query {QueryId} with status {StatusCode}: {Body}",
                request.QueryId,
                (int)response.StatusCode,
                body);

            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Coordinator accepted async query {QueryId}", request.QueryId);
    }

    public async Task<QueryPlanDetails> PlanAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("coordinator.client.plan", ActivityKind.Client);
        activity?.SetTag("query.id", request.QueryId.ToString("D"));

        var payload = CoordinatorQueryPayload.FromRequest(request, async: false);
        var response = await SendAsync(
                HttpMethod.Post,
                "/internal/v1/queries/plan",
                payload,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw await DeserializeExceptionAsync<QueryParseException>(response, cancellationToken)
                .ConfigureAwait(false)
                ?? new QueryParseException("Query rejected by coordinator", string.Empty, ["Invalid query"]);
        }

        response.EnsureSuccessStatusCode();

        var plan = await response.Content
            .ReadFromJsonAsync<QueryPlanDetails>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            throw new InvalidOperationException("Coordinator returned an empty query plan payload.");
        }

        _logger.LogInformation(
            "Coordinator plan resolved for query {QueryId} with {SubQueryCount} sub-queries",
            request.QueryId,
            plan.SubQueries.Count);

        return plan;
    }

    public async IAsyncEnumerable<QueryStreamEvent> StreamExecuteAsync(
        QueryRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("coordinator.client.stream", ActivityKind.Client);
        activity?.SetTag("query.id", request.QueryId.ToString("D"));

        var payload = CoordinatorQueryPayload.FromRequest(request, async: false);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "internal/v1/queries/stream")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        TraceContextPropagator.InjectHttpHeaders(requestMessage, activity: Activity.Current);

        using var response = await _httpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            throw await DeserializeExceptionAsync<InsufficientNodesException>(response, cancellationToken)
                .ConfigureAwait(false)
                ?? new InsufficientNodesException(0, 0);
        }

        if (response.StatusCode == HttpStatusCode.RequestTimeout)
        {
            throw await DeserializeExceptionAsync<QueryTimeoutException>(response, cancellationToken)
                .ConfigureAwait(false)
                ?? new QueryTimeoutException(request.QueryId, request.Timeout ?? TimeSpan.FromSeconds(30));
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw await DeserializeExceptionAsync<QueryParseException>(response, cancellationToken)
                .ConfigureAwait(false)
                ?? new QueryParseException("Query rejected by coordinator", string.Empty, ["Invalid query"]);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await foreach (var streamEvent in ServerSentEventStreamReader
                           .ReadEventsAsync(stream, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return streamEvent;
        }
    }

    public async Task<AdminDashboardStats> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("coordinator.client.admin.dashboard", ActivityKind.Client);
        var response = await SendAsync(HttpMethod.Get, "/internal/v1/admin/dashboard", cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<AdminDashboardStats>(response, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Coordinator returned an empty admin dashboard payload.");
    }

    public async Task<ActiveQueryPage> GetActiveQueriesAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("coordinator.client.admin.active_queries", ActivityKind.Client);
        var response = await SendAsync(
                HttpMethod.Get,
                $"/internal/v1/admin/queries/active?limit={limit}&offset={offset}",
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<ActiveQueryPage>(response, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Coordinator returned an empty active query page payload.");
    }

    public async Task<CancelQueryResult> CancelQueryAsync(Guid queryId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("coordinator.client.admin.cancel_query", ActivityKind.Client);
        activity?.SetTag("query.id", queryId.ToString("D"));

        var response = await SendAsync(
                HttpMethod.Post,
                $"/internal/v1/admin/queries/{queryId:D}/cancel",
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<CancelQueryResult>(response, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Coordinator returned an empty cancel query payload.");
    }

    public async Task<WorkerHealthDashboard> GetWorkerHealthAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("coordinator.client.admin.worker_health", ActivityKind.Client);
        var response = await SendAsync(HttpMethod.Get, "/internal/v1/admin/workers", cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<WorkerHealthDashboard>(response, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Coordinator returned an empty worker health payload.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(method, relativePath.TrimStart('/'));
        TraceContextPropagator.InjectHttpHeaders(requestMessage, activity: Activity.Current);
        return await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        CoordinatorQueryPayload payload,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(method, relativePath.TrimStart('/'))
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        TraceContextPropagator.InjectHttpHeaders(requestMessage, activity: Activity.Current);
        return await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TException?> DeserializeExceptionAsync<TException>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TException : Exception
    {
        try
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<CoordinatorErrorEnvelope>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (envelope is null)
            {
                return null;
            }

            return envelope.ToException() as TException;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record CoordinatorQueryPayload(
        Guid QueryId,
        string Sql,
        IReadOnlyList<QueryParameter> Parameters,
        int? MaxNodes,
        int? TimeoutMs,
        bool Async,
        string FailurePolicyName)
    {
        public static CoordinatorQueryPayload FromRequest(QueryRequest request, bool async) =>
            new(
                request.QueryId,
                request.Sql,
                request.Parameters,
                request.MaxNodes,
                request.Timeout is null ? null : (int)request.Timeout.Value.TotalMilliseconds,
                async,
                request.FailurePolicy.ToString());
    }

    private sealed record CoordinatorErrorEnvelope(
        string Type,
        string Message,
        string? SqlHash,
        IReadOnlyList<string>? ParseErrors,
        int? RequiredShards,
        int? AvailableNodes,
        Guid? QueryId,
        int? TimeoutMs)
    {
        public Exception ToException() => Type switch
        {
            nameof(QueryParseException) => new QueryParseException(
                Message,
                SqlHash ?? string.Empty,
                ParseErrors ?? []),
            nameof(InsufficientNodesException) => new InsufficientNodesException(
                RequiredShards ?? 0,
                AvailableNodes ?? 0),
            nameof(QueryTimeoutException) => new QueryTimeoutException(
                QueryId ?? Guid.Empty,
                TimeSpan.FromMilliseconds(TimeoutMs ?? 0)),
            _ => new InvalidOperationException(Message)
        };
    }
}
