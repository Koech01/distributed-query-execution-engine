using DistributedQuery.Core.Interfaces;
using DistributedQuery.Infrastructure.Grpc;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Worker.Services;

/// <summary>
/// Worker host entry point for the QueryExecution gRPC contract.
/// Delegates to <see cref="QueryExecutionService"/> for protocol mapping and streaming.
/// </summary>
public sealed class WorkerGrpcService : QueryExecutionService
{
    public WorkerGrpcService(
        ILoggerFactory loggerFactory,
        ISubQueryExecutor subQueryExecutor)
        : base(loggerFactory.CreateLogger<QueryExecutionService>(), subQueryExecutor)
    {
    }
}
