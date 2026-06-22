using System.Diagnostics;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Infrastructure.Grpc;

/// <summary>
/// Probes worker gRPC health and HTTP liveness/readiness endpoints for admin dashboards.
/// </summary>
public sealed class WorkerHealthProbe
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Grpc.WorkerHealthProbe");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WorkerHealthProbe> _logger;

    public WorkerHealthProbe(IHttpClientFactory httpClientFactory, ILogger<WorkerHealthProbe> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkerHealthEntry> ProbeAsync(NodeInfo node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        using var activity = ActivitySource.StartActivity("WorkerHealthProbe.Probe", ActivityKind.Client);
        activity?.SetTag("worker.node_id", node.NodeId);

        var liveProbe = await ProbeHttpAsync(
                node,
                "/health/live",
                cancellationToken)
            .ConfigureAwait(false);
        var readyProbe = await ProbeHttpAsync(
                node,
                "/health/ready",
                cancellationToken)
            .ConfigureAwait(false);
        var grpcProbe = await ProbeGrpcAsync(node, cancellationToken).ConfigureAwait(false);

        return new WorkerHealthEntry(
            node.NodeId,
            node.Address,
            node.GrpcPort,
            node.ResolvedHealthPort,
            node.Shards,
            node.Version,
            liveProbe.Status,
            readyProbe.Status,
            grpcProbe.Status,
            liveProbe.LatencyMs,
            readyProbe.LatencyMs,
            grpcProbe.LatencyMs,
            RegisteredInConsul: true);
    }

    private async Task<(WorkerProbeStatus Status, int? LatencyMs)> ProbeHttpAsync(
        NodeInfo node,
        string path,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(WorkerHealthProbe));
            using var response = await client
                .GetAsync($"http://{node.Address}:{node.ResolvedHealthPort}{path}", cancellationToken)
                .ConfigureAwait(false);

            var latencyMs = (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            return response.IsSuccessStatusCode
                ? (WorkerProbeStatus.Healthy, latencyMs)
                : (WorkerProbeStatus.Unhealthy, latencyMs);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Worker HTTP probe failed for node {NodeId} at {Path}",
                node.NodeId,
                path);
            return (WorkerProbeStatus.Unreachable, null);
        }
    }

    private async Task<(WorkerProbeStatus Status, int? LatencyMs)> ProbeGrpcAsync(
        NodeInfo node,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            using var channel = GrpcChannel.ForAddress($"http://{node.Address}:{node.GrpcPort}");
            var client = new QueryExecution.QueryExecutionClient(channel);
            using var call = client.CheckAsync(new HealthCheckRequest(), cancellationToken: cancellationToken);
            var response = await call.ResponseAsync.ConfigureAwait(false);
            var latencyMs = (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            return response.Status == HealthCheckResponse.Types.Status.Serving
                ? (WorkerProbeStatus.Healthy, latencyMs)
                : (WorkerProbeStatus.Unhealthy, latencyMs);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogDebug(ex, "Worker gRPC health probe unreachable for node {NodeId}", node.NodeId);
            return (WorkerProbeStatus.Unreachable, null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Worker gRPC health probe failed for node {NodeId}", node.NodeId);
            return (WorkerProbeStatus.Unhealthy, null);
        }
    }
}
