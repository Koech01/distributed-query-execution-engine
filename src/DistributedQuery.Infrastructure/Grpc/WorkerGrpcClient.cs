using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using CoreColumnDescriptor = DistributedQuery.Core.Models.ColumnDescriptor;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Infrastructure.Grpc;

public sealed class WorkerGrpcClient : IWorkerClient, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Grpc.WorkerGrpcClient");

    private readonly ILogger<WorkerGrpcClient> _logger;
    private readonly GrpcChannelOptions _channelOptions;
    private readonly Func<Uri, QueryExecution.QueryExecutionClient> _clientFactory;
    private readonly ConcurrentDictionary<string, QueryExecution.QueryExecutionClient> _clientPool = new();

    public WorkerGrpcClient(
        ILogger<WorkerGrpcClient> logger,
        GrpcChannelOptions? channelOptions = null,
        Func<Uri, QueryExecution.QueryExecutionClient>? clientFactory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _channelOptions = channelOptions ?? new GrpcChannelOptions();
        _clientFactory = clientFactory ?? CreateClient;
    }

    public async IAsyncEnumerable<PartialResult> ExecuteAsync(
        SubQuery subQuery,
        NodeInfo node,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (subQuery is null)
        {
            throw new ArgumentNullException(nameof(subQuery));
        }

        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        using var activity = ActivitySource.StartActivity("WorkerGrpcClient.ExecuteAsync", ActivityKind.Client);
        activity?.SetTag("sub_query_id", subQuery.SubQueryId.ToString());
        activity?.SetTag("parent_query_id", subQuery.ParentQueryId.ToString());
        activity?.SetTag("shard.index", subQuery.ShardIndex);
        activity?.SetTag("worker.node_id", node.NodeId);

        var nodeUri = BuildUri(node);
        var client = _clientPool.GetOrAdd(nodeUri.AbsoluteUri, _ => _clientFactory(nodeUri));

        _logger.LogInformation(
            "Sending sub-query {SubQueryId} to worker {WorkerNodeId} at {WorkerAddress}",
            subQuery.SubQueryId,
            node.NodeId,
            nodeUri);

        var channel = Channel.CreateUnbounded<PartialResult>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        var producer = ProduceResultsAsync(
            client,
            subQuery,
            node,
            BuildRequest(subQuery),
            channel.Writer,
            activity,
            cancellationToken);

        await foreach (var partial in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return partial;
        }

        await producer;
    }

    public void Dispose()
    {
        foreach (var entry in _clientPool)
        {
            if (entry.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _clientPool.Clear();
    }

    private async Task ProduceResultsAsync(
        QueryExecution.QueryExecutionClient client,
        SubQuery subQuery,
        NodeInfo node,
        SubQueryRequest request,
        ChannelWriter<PartialResult> writer,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var state = new StreamState(subQuery);

        try
        {
            var deadline = request.TimeoutMs > 0
                ? DateTime.UtcNow.AddMilliseconds(request.TimeoutMs)
                : (DateTime?)null;
            using var call = client.ExecuteSubQuery(request, deadline: deadline, cancellationToken: cancellationToken);

            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                switch (response.PayloadCase)
                {
                    case PartialResultResponse.PayloadOneofCase.Meta:
                        state.Columns = response.Meta.Columns
                            .Select(column => new CoreColumnDescriptor(column.Name, column.DataType, column.Nullable))
                            .ToArray();
                        state.ExecutionMs = response.Meta.ExecutionMs;
                        break;

                    case PartialResultResponse.PayloadOneofCase.Chunk:
                    {
                        var chunkRows = response.Chunk.Rows
                            .Select(row => (IReadOnlyList<string>)row.Values.ToArray())
                            .ToArray();

                        if (chunkRows.Length == 0)
                        {
                            break;
                        }

                        state.YieldedAny = true;
                        await writer.WriteAsync(
                            PartialResult.Success(
                                subQuery.SubQueryId,
                                subQuery.ParentQueryId,
                                subQuery.ShardIndex,
                                state.Columns,
                                chunkRows,
                                state.ExecutionMs),
                            cancellationToken);
                        break;
                    }

                    case PartialResultResponse.PayloadOneofCase.Error:
                        state.TerminalError = response.Error.Message;
                        state.TerminalStatus = state.YieldedAny ? PartialResultStatus.Degraded : PartialResultStatus.Failed;
                        _logger.LogWarning(
                            "Worker {WorkerNodeId} reported sub-query {SubQueryId} error after partial delivery: {ErrorMessage}",
                            node.NodeId,
                            subQuery.SubQueryId,
                            state.TerminalError);
                        break;

                    case PartialResultResponse.PayloadOneofCase.None:
                        _logger.LogWarning(
                            "Worker {WorkerNodeId} returned an empty payload for sub-query {SubQueryId}",
                            node.NodeId,
                            subQuery.SubQueryId);
                        break;
                }
            }

            if (state.TerminalError is not null)
            {
                await writer.WriteAsync(
                    state.CreateTerminalResult(state.TerminalStatus, state.TerminalError),
                    cancellationToken);
            }
            else if (!state.YieldedAny)
            {
                _logger.LogInformation(
                    "Sub-query {SubQueryId} completed successfully on worker {WorkerNodeId} with no rows",
                    subQuery.SubQueryId,
                    node.NodeId);

                await writer.WriteAsync(
                    PartialResult.Success(
                        subQuery.SubQueryId,
                        subQuery.ParentQueryId,
                        subQuery.ShardIndex,
                        state.Columns,
                        Array.Empty<IReadOnlyList<string>>(),
                        state.ExecutionMs),
                    cancellationToken);
            }
        }
        catch (RpcException rpcException) when (rpcException.StatusCode is StatusCode.DeadlineExceeded or StatusCode.Cancelled)
        {
            activity?.SetStatus(ActivityStatusCode.Error, rpcException.Message);
            _logger.LogWarning(
                "gRPC call for sub-query {SubQueryId} to worker {WorkerNodeId} did not complete: {GrpcStatusCode} {ErrorMessage}",
                subQuery.SubQueryId,
                node.NodeId,
                rpcException.StatusCode,
                rpcException.Status.Detail ?? rpcException.Message);

            await writer.WriteAsync(
                state.CreateTerminalResult(
                    state.YieldedAny ? PartialResultStatus.Degraded : PartialResultStatus.TimedOut,
                    rpcException.Status.Detail ?? rpcException.Message),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Query execution was canceled.");
            _logger.LogWarning(
                "Sub-query {SubQueryId} canceled while executing on worker {WorkerNodeId}",
                subQuery.SubQueryId,
                node.NodeId);

            await writer.WriteAsync(
                state.CreateTerminalResult(
                    state.YieldedAny ? PartialResultStatus.Degraded : PartialResultStatus.TimedOut,
                    "Query execution was canceled."),
                cancellationToken);
        }
        catch (RpcException rpcException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, rpcException.Message);
            _logger.LogWarning(
                "gRPC call for sub-query {SubQueryId} to worker {WorkerNodeId} failed: {GrpcStatusCode} {ErrorMessage}",
                subQuery.SubQueryId,
                node.NodeId,
                rpcException.StatusCode,
                rpcException.Status.Detail ?? rpcException.Message);

            await writer.WriteAsync(
                state.CreateTerminalResult(
                    state.YieldedAny ? PartialResultStatus.Degraded : PartialResultStatus.Failed,
                    rpcException.Status.Detail ?? rpcException.Message),
                cancellationToken);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private QueryExecution.QueryExecutionClient CreateClient(Uri address)
    {
        var channel = GrpcChannel.ForAddress(address, _channelOptions);
        var callInvoker = channel
            .Intercept(new TracingClientInterceptor(), new MetricsClientInterceptor());
        return new QueryExecution.QueryExecutionClient(callInvoker);
    }

    private static Uri BuildUri(NodeInfo node)
    {
        if (Uri.TryCreate(node.Address, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri)
        {
            if (uri.Port == -1)
            {
                return new UriBuilder(uri) { Port = node.GrpcPort }.Uri;
            }

            return uri;
        }

        return new Uri($"http://{node.Address}:{node.GrpcPort}");
    }

    private static SubQueryRequest BuildRequest(SubQuery subQuery)
    {
        var request = new SubQueryRequest
        {
            SubQueryId = subQuery.SubQueryId.ToString("D"),
            ParentQueryId = subQuery.ParentQueryId.ToString("D"),
            Sql = subQuery.Sql,
            ShardIndex = subQuery.ShardIndex,
            TotalShards = subQuery.TotalShards,
            TimeoutMs = Math.Max(0, subQuery.TimeoutMs)
        };

        request.Parameters.AddRange(subQuery.Parameters.Select(parameter => new QueryParameter
        {
            Name = parameter.Name,
            Type = parameter.Type,
            Value = parameter.Value
        }));

        return request;
    }

    private sealed class StreamState
    {
        public StreamState(SubQuery subQuery)
        {
            SubQuery = subQuery;
        }

        public SubQuery SubQuery { get; }
        public IReadOnlyList<CoreColumnDescriptor> Columns { get; set; } = Array.Empty<CoreColumnDescriptor>();
        public long ExecutionMs { get; set; }
        public bool YieldedAny { get; set; }
        public string? TerminalError { get; set; }
        public PartialResultStatus TerminalStatus { get; set; } = PartialResultStatus.Failed;

        public PartialResult CreateTerminalResult(PartialResultStatus status, string errorMessage) =>
            new(
                SubQuery.SubQueryId,
                SubQuery.ParentQueryId,
                SubQuery.ShardIndex,
                status,
                Columns,
                Array.Empty<IReadOnlyList<string>>(),
                ExecutionMs,
                errorMessage);
    }
}
