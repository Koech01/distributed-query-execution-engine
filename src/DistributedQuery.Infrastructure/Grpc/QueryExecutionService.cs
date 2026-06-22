using System.Diagnostics;
using Grpc.Core;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using CoreQueryParameter = DistributedQuery.Core.Models.QueryParameter;
using DistributedQuery.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Infrastructure.Grpc;

public class QueryExecutionService : QueryExecution.QueryExecutionBase
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Infrastructure.Grpc.QueryExecutionService");
    private readonly ILogger<QueryExecutionService> _logger;
    private readonly ISubQueryExecutor _subQueryExecutor;

    public QueryExecutionService(
        ILogger<QueryExecutionService> logger,
        ISubQueryExecutor subQueryExecutor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subQueryExecutor = subQueryExecutor ?? throw new ArgumentNullException(nameof(subQueryExecutor));
    }

    public override async Task ExecuteSubQuery(
        SubQueryRequest request,
        IServerStreamWriter<PartialResultResponse> responseStream,
        ServerCallContext context)
    {
        if (request is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Request cannot be null."));
        }

        using var activity = ActivitySource.StartActivity("QueryExecutionService.ExecuteSubQuery", ActivityKind.Server);
        activity?.SetTag("grpc.method", nameof(ExecuteSubQuery));
        activity?.SetTag("sub_query_id", request.SubQueryId);
        activity?.SetTag("parent_query_id", request.ParentQueryId);
        activity?.SetTag("shard_index", request.ShardIndex);

        var subQuery = BuildSubQuery(request);
        using var linkedCts = CreateLinkedCancellationTokenSource(request, context);

        _logger.LogInformation(
            "Received sub-query {SubQueryId} for shard {ShardIndex}",
            subQuery.SubQueryId,
            subQuery.ShardIndex);

        try
        {
            await foreach (var result in _subQueryExecutor.ExecuteAsync(subQuery, linkedCts.Token))
            {
                await WriteResultAsync(result, responseStream, linkedCts.Token);
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Sub-query execution was canceled by the caller.");
            _logger.LogWarning(
                "Sub-query {SubQueryId} execution was canceled by the caller.",
                subQuery.SubQueryId);

            throw new RpcException(new Status(StatusCode.Cancelled, "Sub-query execution was canceled."));
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Sub-query execution timed out or was canceled.");
            _logger.LogWarning(
                "Sub-query {SubQueryId} execution was canceled or timed out.",
                subQuery.SubQueryId);

            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Sub-query execution timed out or was canceled."));
        }
        catch (QueryTimeoutException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(
                "Sub-query {SubQueryId} timed out after {TimeoutMs}ms.",
                subQuery.SubQueryId,
                ex.Timeout.TotalMilliseconds);

            throw new RpcException(new Status(StatusCode.DeadlineExceeded, ex.Message));
        }
        catch (ShardExecutionException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex,
                "Sub-query {SubQueryId} failed on shard {ShardIndex}.",
                subQuery.SubQueryId,
                ex.ShardIndex);

            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
        catch (ArgumentException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex,
                "Sub-query {SubQueryId} failed during execution.",
                subQuery.SubQueryId);

            throw new RpcException(new Status(StatusCode.Internal, "Worker failed to execute sub-query."));
        }
    }

    public override Task<HealthCheckResponse> Check(HealthCheckRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HealthCheckResponse
        {
            Status = HealthCheckResponse.Types.Status.Serving
        });
    }

    private static CancellationTokenSource CreateLinkedCancellationTokenSource(SubQueryRequest request, ServerCallContext context)
    {
        if (request.TimeoutMs <= 0)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        }

        var timeoutSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.TimeoutMs));
        return CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutSource.Token);
    }

    private static SubQuery BuildSubQuery(SubQueryRequest request)
    {
        if (!Guid.TryParse(request.SubQueryId, out var subQueryId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "SubQueryId must be a valid GUID."));
        }

        if (!Guid.TryParse(request.ParentQueryId, out var parentQueryId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ParentQueryId must be a valid GUID."));
        }

        if (string.IsNullOrWhiteSpace(request.Sql))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Sql must be provided."));
        }

        if (request.ShardIndex < 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ShardIndex must be non-negative."));
        }

        if (request.TotalShards <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "TotalShards must be greater than zero."));
        }

        var parameters = request.Parameters
            .Select(parameter => new CoreQueryParameter(parameter.Name, parameter.Type, parameter.Value))
            .ToArray();

        return new SubQuery(
            subQueryId,
            parentQueryId,
            request.Sql,
            string.Empty,
            request.ShardIndex,
            request.TotalShards,
            parameters,
            (int)Math.Min(int.MaxValue, Math.Max(0, request.TimeoutMs)));
    }

    private static async Task WriteResultAsync(
        PartialResult partialResult,
        IServerStreamWriter<PartialResultResponse> responseStream,
        CancellationToken cancellationToken)
    {
        if (partialResult is null)
        {
            throw new ArgumentNullException(nameof(partialResult));
        }

        if (partialResult.Columns.Count > 0)
        {
            var meta = new PartialResultMeta
            {
                ExecutionMs = partialResult.ExecutionMs,
                RowCount = partialResult.Rows.Count
            };

            meta.Columns.AddRange(partialResult.Columns.Select(column => new ColumnDescriptor
            {
                Name = column.Name,
                DataType = column.DataType,
                Nullable = column.Nullable
            }));

            await responseStream.WriteAsync(new PartialResultResponse
            {
                SubQueryId = partialResult.SubQueryId.ToString("D"),
                Meta = meta
            },
                cancellationToken);
        }

        foreach (var row in partialResult.Rows)
        {
            var chunk = new PartialResultChunk();
            chunk.Rows.Add(new ResultRow { Values = { row } });

            await responseStream.WriteAsync(new PartialResultResponse
            {
                SubQueryId = partialResult.SubQueryId.ToString("D"),
                Chunk = chunk
            },
                cancellationToken);
        }

        if (!partialResult.IsSuccess)
        {
            await responseStream.WriteAsync(new PartialResultResponse
            {
                SubQueryId = partialResult.SubQueryId.ToString("D"),
                Error = new PartialResultError
                {
                    Code = partialResult.Status.ToString().ToUpperInvariant(),
                    Message = partialResult.ErrorMessage ?? "Worker failed to execute sub-query."
                }
            },
                cancellationToken);
        }
    }
}
