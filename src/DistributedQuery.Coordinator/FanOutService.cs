using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DistributedQuery.Core.Models;
using DistributedQuery.Core.Interfaces;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace DistributedQuery.Coordinator;

public sealed class FanOutService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Coordinator");

    private readonly IWorkerClient _workerClient;
    private readonly CoordinatorOptions _options;
    private readonly ILogger<FanOutService> _logger;
    private readonly ConcurrentDictionary<string, ResiliencePipeline<IReadOnlyList<PartialResult>>> _nodePipelines = new();

    public FanOutService(
        IWorkerClient workerClient,
        IOptions<CoordinatorOptions> options,
        ILogger<FanOutService> logger)
    {
        _workerClient = workerClient ?? throw new ArgumentNullException(nameof(workerClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<PartialResult>> ExecuteAsync(
        IReadOnlyList<(SubQuery SubQuery, NodeInfo Node)> assignments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        using var activity = ActivitySource.StartActivity("coordinator.fanout.execute", ActivityKind.Internal);
        activity?.SetTag("fanout.size", assignments.Count);

        using var semaphore = new SemaphoreSlim(_options.FanOut.MaxConcurrentWorkerCalls, _options.FanOut.MaxConcurrentWorkerCalls);
        var tasks = assignments.Select(assignment => ExecuteSingleAsync(assignment.SubQuery, assignment.Node, semaphore, cancellationToken)).ToArray();
        var allResults = await Task.WhenAll(tasks).ConfigureAwait(false);
        return allResults.SelectMany(static item => item).ToArray();
    }

    public async IAsyncEnumerable<PartialResult> StreamAsync(
        IReadOnlyList<(SubQuery SubQuery, NodeInfo Node)> assignments,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        if (assignments.Count == 0)
        {
            yield break;
        }

        using var activity = ActivitySource.StartActivity("coordinator.fanout.stream", ActivityKind.Internal);
        activity?.SetTag("fanout.size", assignments.Count);

        var channel = Channel.CreateUnbounded<PartialResult>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        using var semaphore = new SemaphoreSlim(_options.FanOut.MaxConcurrentWorkerCalls, _options.FanOut.MaxConcurrentWorkerCalls);
        var pumpTasks = assignments
            .Select(assignment => PumpWorkerStreamAsync(assignment.SubQuery, assignment.Node, semaphore, channel.Writer, cancellationToken))
            .ToArray();

        var completionTask = Task.WhenAll(pumpTasks).ContinueWith(
            static (task, state) =>
            {
                var writer = (ChannelWriter<PartialResult>)state!;
                if (task.IsFaulted)
                {
                    writer.TryComplete(task.Exception);
                }
                else
                {
                    writer.TryComplete();
                }
            },
            channel.Writer,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        await foreach (var partial in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return partial;
        }

        await completionTask.ConfigureAwait(false);
    }

    private async Task PumpWorkerStreamAsync(
        SubQuery subQuery,
        NodeInfo node,
        SemaphoreSlim semaphore,
        ChannelWriter<PartialResult> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            using var activity = ActivitySource.StartActivity("coordinator.fanout.worker_call", ActivityKind.Client);
            activity?.SetTag("query.id", subQuery.ParentQueryId.ToString("D"));
            activity?.SetTag("sub_query_id", subQuery.SubQueryId.ToString("D"));
            activity?.SetTag("shard.index", subQuery.ShardIndex);
            activity?.SetTag("worker.node_id", node.NodeId);

            var hasChunks = false;
            await foreach (var partial in _workerClient.ExecuteAsync(subQuery, node, cancellationToken)
                               .ConfigureAwait(false))
            {
                hasChunks = true;
                await writer.WriteAsync(partial, cancellationToken).ConfigureAwait(false);
            }

            if (!hasChunks)
            {
                await writer.WriteAsync(
                    PartialResult.Failure(
                        subQuery.SubQueryId,
                        subQuery.ParentQueryId,
                        subQuery.ShardIndex,
                        PartialResultStatus.Failed,
                        "Worker call returned no partial results."),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning(
                "Circuit is open for worker {WorkerNodeId}. Marking shard {ShardIndex} as failed.",
                node.NodeId,
                subQuery.ShardIndex);
            await writer.WriteAsync(
                PartialResult.Failure(
                    subQuery.SubQueryId,
                    subQuery.ParentQueryId,
                    subQuery.ShardIndex,
                    PartialResultStatus.Failed,
                    "Circuit open"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutRejectedException)
        {
            _logger.LogWarning(
                "Worker call timed out for sub-query {SubQueryId} on worker {WorkerNodeId}",
                subQuery.SubQueryId,
                node.NodeId);
            await writer.WriteAsync(
                PartialResult.Failure(
                    subQuery.SubQueryId,
                    subQuery.ParentQueryId,
                    subQuery.ShardIndex,
                    PartialResultStatus.TimedOut,
                    $"Timed out after {_options.FanOut.PerWorkerTimeoutMs}ms"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await writer.WriteAsync(
                PartialResult.Failure(
                    subQuery.SubQueryId,
                    subQuery.ParentQueryId,
                    subQuery.ShardIndex,
                    PartialResultStatus.TimedOut,
                    "Fan-out cancelled."),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Worker call failed for sub-query {SubQueryId} on node {WorkerNodeId}",
                subQuery.SubQueryId,
                node.NodeId);
            await writer.WriteAsync(
                PartialResult.Failure(
                    subQuery.SubQueryId,
                    subQuery.ParentQueryId,
                    subQuery.ShardIndex,
                    PartialResultStatus.Failed,
                    ex.Message),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (semaphore.CurrentCount < _options.FanOut.MaxConcurrentWorkerCalls)
            {
                semaphore.Release();
            }
        }
    }

    private async Task<IReadOnlyList<PartialResult>> ExecuteSingleAsync(
        SubQuery subQuery,
        NodeInfo node,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            using var activity = ActivitySource.StartActivity("coordinator.fanout.worker_call", ActivityKind.Client);
            activity?.SetTag("query.id", subQuery.ParentQueryId.ToString("D"));
            activity?.SetTag("sub_query_id", subQuery.SubQueryId.ToString("D"));
            activity?.SetTag("shard.index", subQuery.ShardIndex);
            activity?.SetTag("worker.node_id", node.NodeId);

            var pipeline = _nodePipelines.GetOrAdd(node.NodeId, _ => BuildPipeline(node.NodeId));

            return await pipeline.ExecuteAsync(async (ct) =>
            {
                var results = new List<PartialResult>();
                await foreach (var partial in _workerClient.ExecuteAsync(subQuery, node, ct).WithCancellation(ct))
                {
                    results.Add(partial);
                }

                if (results.Count == 0)
                {
                    results.Add(PartialResult.Failure(
                        subQuery.SubQueryId,
                        subQuery.ParentQueryId,
                        subQuery.ShardIndex,
                        PartialResultStatus.Failed,
                        "Worker call returned no partial results."));
                }

                return (IReadOnlyList<PartialResult>)results;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning(
                "Circuit is open for worker {WorkerNodeId}. Marking shard {ShardIndex} as failed.",
                node.NodeId,
                subQuery.ShardIndex);
            return
            [
                PartialResult.Failure(
                    subQuery.SubQueryId,
                    subQuery.ParentQueryId,
                    subQuery.ShardIndex,
                    PartialResultStatus.Failed,
                    "Circuit open")
            ];
        }
        catch (TimeoutRejectedException)
        {
            _logger.LogWarning(
                "Worker call timed out for sub-query {SubQueryId} on worker {WorkerNodeId}",
                subQuery.SubQueryId,
                node.NodeId);
            return
            [
                PartialResult.Failure(
                    subQuery.SubQueryId,
                    subQuery.ParentQueryId,
                    subQuery.ShardIndex,
                    PartialResultStatus.TimedOut,
                    $"Timed out after {_options.FanOut.PerWorkerTimeoutMs}ms")
            ];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return
            [
                PartialResult.Failure(
                    subQuery.SubQueryId,
                    subQuery.ParentQueryId,
                    subQuery.ShardIndex,
                    PartialResultStatus.TimedOut,
                    "Fan-out cancelled.")
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Worker call failed for sub-query {SubQueryId} on node {WorkerNodeId}",
                subQuery.SubQueryId,
                node.NodeId);
            return
            [
                PartialResult.Failure(
                    subQuery.SubQueryId,
                    subQuery.ParentQueryId,
                    subQuery.ShardIndex,
                    PartialResultStatus.Failed,
                    ex.Message)
            ];
        }
        finally
        {
            if (semaphore.CurrentCount < _options.FanOut.MaxConcurrentWorkerCalls)
            {
                semaphore.Release();
            }
        }
    }

    private ResiliencePipeline<IReadOnlyList<PartialResult>> BuildPipeline(string nodeId)
    {
        var retryOptions = new RetryStrategyOptions<IReadOnlyList<PartialResult>>
        {
            MaxRetryAttempts = _options.Resilience.RetryCount,
            Delay = TimeSpan.FromMilliseconds(_options.Resilience.RetryBaseDelayMs),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder<IReadOnlyList<PartialResult>>()
                .Handle<RpcException>(static ex => ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.ResourceExhausted),
            OnRetry = args =>
            {
                _logger.LogWarning(
                    "Retrying worker call to node {WorkerNodeId}. Attempt {AttemptNumber}.",
                    nodeId,
                    args.AttemptNumber + 1);
                return default;
            }
        };

        var circuitBreakerOptions = new CircuitBreakerStrategyOptions<IReadOnlyList<PartialResult>>
        {
            FailureRatio = 1.0,
            MinimumThroughput = _options.Resilience.CircuitBreakerFailureThreshold,
            SamplingDuration = TimeSpan.FromSeconds(_options.Resilience.CircuitBreakerSamplingDurationSeconds),
            BreakDuration = TimeSpan.FromSeconds(_options.Resilience.CircuitBreakerBreakDurationSeconds),
            ShouldHandle = new PredicateBuilder<IReadOnlyList<PartialResult>>()
                .Handle<RpcException>(static ex => ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.ResourceExhausted)
                .Handle<TimeoutRejectedException>()
        };

        var timeoutOptions = new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromMilliseconds(_options.FanOut.PerWorkerTimeoutMs)
        };

        return new ResiliencePipelineBuilder<IReadOnlyList<PartialResult>>()
            .AddRetry(retryOptions)
            .AddCircuitBreaker(circuitBreakerOptions)
            .AddTimeout(timeoutOptions)
            .Build();
    }
}
